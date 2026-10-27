#!/usr/bin/env python3
"""Create a small restaurant dataset through the deployed API. Run explicitly.

Prompts for the existing Admin login. Passwords and tokens stay in memory.
Progress is recorded under ignored .build/ so an interrupted run can be resumed.
Run one copy at a time. No accounts or reservations are created.
Use --assign-waiter to assign an already registered waiter to the demo location.
"""
import argparse
from datetime import datetime, timedelta, timezone
from getpass import getpass
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1]
ADDRESS = "Marathon Demo Restaurant, 1 Learning Street"
DISHES = [
    ("Marathon Demo Salad", "Appetizer", 8, "Fresh vegetables and herbs.", "250 g", "180 kcal"),
    ("Marathon Demo Pasta", "MainCourse", 14, "Pasta with tomato sauce.", "350 g", "450 kcal"),
    ("Marathon Demo Cheesecake", "Dessert", 7, "Cheesecake with berries.", "150 g", "320 kcal"),
]


class Api:
    def __init__(self, url):
        self.url = url.rstrip("/")
        self.token = None

    def request(self, method, path, body=None):
        headers = {"Accept": "application/json"}
        if body is not None:
            headers["Content-Type"] = "application/json"
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        request = Request(
            self.url + path,
            data=json.dumps(body).encode() if body is not None else None,
            headers=headers,
            method=method,
        )
        try:
            with urlopen(request, timeout=40) as response:
                content = response.read()
                return json.loads(content) if content else None
        except HTTPError as error:
            hints = {
                401: "Check your login, or sign in again if the token expired.",
                403: "Check Admin group membership; an Admin login is required.",
                404: "Check that the requested resource and waiter account exist.",
                409: "This record already exists. Review its current assignment before trying again.",
            }
            raise SystemExit(f"API {method} request failed: HTTP {error.code}. " + hints.get(error.code, "Inspect the api-handler CloudWatch logs. Progress is saved locally.")) from None
        except URLError:
            raise SystemExit("Could not reach the API. Check connectivity. No automatic write retries were made.") from None


def seed(api, progress, save):
    locations = api.request("GET", "/locations")
    matches = [location for location in locations if location["address"] == ADDRESS]
    if len(matches) > 1:
        raise SystemExit("Multiple demo locations found. Review them before seeding again.")
    if matches:
        location = matches[0]
        print("Reusing demo location.")
    else:
        if progress.get("location_id"):
            raise SystemExit("The saved demo location no longer exists. Review .build/demo-data.json before starting a new dataset.")
        location = api.request("POST", "/locations", {
            "address": ADDRESS,
            "description": "Demo restaurant for EPAM deployment verification.",
            "totalCapacity": 12,
            "averageOccupancy": "0",
            "rating": 0,
            "imageBase64": None,
        })
        print("Created demo location.")
    if progress.get("location_id") not in (None, location["id"]):
        raise SystemExit("The demo location changed. Review the saved progress before continuing.")
    progress["location_id"] = location["id"]
    save()

    # Use the public availability endpoint; the all-tables route requires Waiter.
    start = (datetime.now(timezone.utc) + timedelta(days=1)).replace(hour=10, minute=0, second=0, microsecond=0)
    query = urlencode({"locationId": location["id"], "startTime": start.isoformat(), "capacity": 0})
    tables = api.request("GET", "/available-tables?" + query)
    saved_tables = progress.setdefault("tables", {})
    for number in [1, 2, 3]:
        if str(number) in saved_tables:
            print(f"Keeping recorded demo table {number}.")
            continue
        existing = next((table for table in tables if table["tableNumber"] == number), None)
        table = existing or api.request("POST", "/tables", {
            "locationId": location["id"], "capacity": 4, "tableNumber": number,
        })
        saved_tables[str(number)] = table["id"]
        save()
        print(f"{'Reused' if existing else 'Created'} demo table {number}.")

    existing_dishes = api.request("GET", f"/locations/{location['id']}/dishes")
    saved_dishes = progress.setdefault("dishes", {})
    for name, kind, price, description, weight, calories in DISHES:
        existing = next((dish for dish in existing_dishes if dish["name"] == name), None)
        if not existing and name in saved_dishes:
            raise SystemExit("A recorded dish is no longer linked to the demo location. Review the saved progress before recreating it.")
        dish = existing or api.request("POST", "/dishes", {
            "name": name, "description": description, "dishType": kind,
            "price": price, "weight": weight, "calories": calories,
            "proteins": "0 g", "fats": "0 g", "carbohydrates": "0 g",
            "vitamins": "Demo data", "state": "Available",
            "imageBase64": None, "locationIds": [location["id"]],
        })
        saved_dishes[name] = dish["id"]
        save()
        print(f"{'Reused' if existing else 'Created'} {name}.")


def assign_waiter(api, progress, waiter_email, admin_email):
    waiter_email = waiter_email.strip().lower()
    if waiter_email == admin_email.strip().lower():
        raise SystemExit("Use the separate waiter account, not your Admin account.")
    if not waiter_email or "@" not in waiter_email:
        raise SystemExit("Enter the email used to register the waiter account.")
    location_id = progress.get("location_id")
    if not location_id:
        raise SystemExit("No saved demo location found. Run the demo-data seed first.")
    location = api.request("GET", f"/locations/{location_id}")
    if location.get("address") != ADDRESS:
        raise SystemExit("The saved location is not the expected demo restaurant. Review local progress first.")
    api.request("POST", "/waiters", {"email": waiter_email, "locationId": location_id})
    print("Waiter assigned to the demo restaurant; the API created the waiter record and added the Waiter group.")
    print("Sign in as the waiter again to obtain a token with the new group membership.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assign-waiter", action="store_true", help="Assign an existing registered account to the saved demo location, without seeding data again.")
    args = parser.parse_args()
    result = subprocess.run([
        "terraform", f"-chdir={ROOT / 'infra/terraform'}", "output", "-raw", "backend_url",
    ], capture_output=True, text=True)
    if result.returncode:
        raise SystemExit("Could not read backend_url. Run from your terminal after Terraform apply.")
    url = result.stdout.strip()
    if not url.startswith("https://"):
        raise SystemExit("Expected an HTTPS backend URL from Terraform.")
    api = Api(url)
    fingerprint = hashlib.sha256(url.encode()).hexdigest()
    state_file = ROOT / ".build/demo-data.json"
    progress = json.loads(state_file.read_text()) if state_file.exists() else {"backend_fingerprint": fingerprint}
    if progress.get("backend_fingerprint") != fingerprint:
        raise SystemExit("Saved demo data belongs to another deployment. Review .build/demo-data.json first.")
    if args.assign_waiter and not progress.get("location_id"):
        raise SystemExit("No saved demo location found. Run the demo-data seed first.")

    print("This assigns an existing waiter account to your demo location." if args.assign_waiter else "This adds 1 demo location, 3 tables, and 3 dishes to your deployed application.")
    email = input("Existing Admin email: ").strip()
    password = getpass("Password (hidden): ")
    login = api.request("POST", "/signin", {"email": email, "password": password})
    del password
    api.token = login["idToken"]
    me = api.request("GET", "/me")
    if me.get("role") != "Admin":
        raise SystemExit("This account is not an Admin. Add it to the Admin group, then run again.")

    if args.assign_waiter:
        waiter_email = input("Registered waiter email (the second account): ")
        assign_waiter(api, progress, waiter_email, email)
        return 0

    def save():
        state_file.parent.mkdir(parents=True, exist_ok=True)
        temporary = state_file.with_suffix(".tmp")
        temporary.write_text(json.dumps(progress, indent=2) + "\n")
        temporary.replace(state_file)

    seed(api, progress, save)
    print("Demo dataset ready. Refresh the website's menu and locations pages.")
    print("A registered waiter must still be assigned before testing customer bookings.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        raise SystemExit("\nStopped. Any completed API writes remain; rerun to resume.") from None
