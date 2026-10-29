#!/usr/bin/env python3
"""Inspect group participants, or explicitly remove one with --remove-guest.

By default reads existing data only. --remove-guest deletes the selected guest
session and its cart. Passwords and login tokens are not printed or saved.
"""
import argparse
from getpass import getpass
import json
from pathlib import Path
import subprocess
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import quote, unquote, urlsplit
from urllib.request import Request, urlopen
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]


def invite_token(value):
    value = value.strip()
    if value.startswith(("https://", "http://")):
        path = urlsplit(value).path.rstrip("/")
        if "/join/" not in path:
            raise SystemExit("Expected the reservation's /join/... invite link.")
        value = unquote(path.rsplit("/join/", 1)[1])
    if not value:
        raise SystemExit("An invite link or token is required.")
    return value


def request(base_url, method, path, body=None, token=None):
    headers = {"Accept": "application/json"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = Request(base_url + path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    try:
        with urlopen(req, timeout=40) as response:
            return response.status, json.loads(response.read())
    except HTTPError as error:
        try:
            result = json.loads(error.read())
        except (json.JSONDecodeError, UnicodeDecodeError):
            result = {"message": "Non-JSON error response"}
        return error.code, result
    except (URLError, TimeoutError):
        raise SystemExit("Could not confirm the API response. Check connectivity and current state before retrying.") from None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--anonymous", action="store_true", help="Call without a login token; expect HTTP 401.")
    parser.add_argument("--missing-group", action="store_true", help="Use a new random group ID; authenticated callers should receive HTTP 404.")
    parser.add_argument("--group-id", help="Use a known group ID directly instead of resolving an invite link.")
    parser.add_argument("--remove-guest", action="store_true", help="Delete a guest session and show the updated snapshot (level 3).")
    args = parser.parse_args()
    if args.missing_group and args.group_id:
        parser.error("Choose either --missing-group or --group-id.")
    result = subprocess.run([
        "terraform", f"-chdir={ROOT / 'infra/terraform'}", "output", "-raw", "backend_url",
    ], capture_output=True, text=True)
    if result.returncode or not result.stdout.strip().startswith("https://"):
        raise SystemExit("Could not read the deployed HTTPS backend_url from Terraform.")
    base_url = result.stdout.strip().rstrip("/")

    token = None
    if not args.anonymous:
        email = input("Account email (Owner for success; another account to test rejection): ").strip()
        password = getpass("Password (hidden): ")
        status, login = request(base_url, "POST", "/signin", {"email": email, "password": password})
        del password
        if status != 200 or not login.get("idToken"):
            raise SystemExit(f"Sign-in failed: HTTP {status}. No credentials were printed or saved.")
        token = login["idToken"]
        del login

    if args.missing_group:
        group_id = str(uuid4())
    elif args.group_id:
        group_id = args.group_id
    elif args.remove_guest:
        group_id = input("Group reservation ID (kept local): ").strip()
        if not group_id:
            raise SystemExit("A group reservation ID is required.")
    else:
        invitation = invite_token(input("Paste the reservation invite link or token (kept local): "))
        status, group = request(base_url, "GET", "/guest/invites/" + quote(invitation, safe=""))
        if status != 200:
            raise SystemExit(f"Invite lookup failed: HTTP {status}. Use an active group invite or --group-id.")
        group_id = group.get("groupReservationId") or group.get("GroupReservationId")
        if not group_id:
            raise SystemExit("The invite response did not contain a group reservation ID.")

    path = "/guest/groups/" + quote(group_id, safe="") + "/sessions"
    if args.remove_guest:
        session_id = input("Guest session ID (Enter to choose from the current list): ").strip()
        if not session_id:
            status, current = request(base_url, "GET", path, token=token)
            if status != 200:
                print(f"Participant lookup: HTTP {status}")
                print(json.dumps(current, indent=2, ensure_ascii=False))
                raise SystemExit("For the DELETE permission check, rerun and enter the guest session ID directly.")
            guests = [p for p in current.get("participants", []) if p.get("type") == "Guest"]
            if not guests:
                raise SystemExit("There are no guests to remove. The Owner session is not removable.")
            for number, guest in enumerate(guests, 1):
                print(f"{number}. {guest.get('name')} — total {guest.get('total')}, submitted: {guest.get('isSubmitted')}")
            try:
                number = int(input("Guest number to remove: "))
            except ValueError:
                raise SystemExit("Enter a guest number from the list.") from None
            if not 1 <= number <= len(guests):
                raise SystemExit("Guest number is outside the list.")
            session_id = guests[number - 1]["id"]
        print(f"Selected guest session ID (keep local for the permission check): {session_id}")
        print("Deleting this guest session and its cart.")
        status, snapshot = request(base_url, "DELETE", path + "/" + quote(session_id, safe=""), token=token)
        label = "Guest-removal endpoint"
    else:
        status, snapshot = request(base_url, "GET", path, token=token)
        label = "Participant-list endpoint"
    print(f"{label}: HTTP {status}")
    print(json.dumps(snapshot, indent=2, ensure_ascii=False))
    print("Keep the snapshot local if it contains real guest names or other private data.")
    return 0 if status == 200 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        raise SystemExit("\nStopped. If removal was already sent, check the group before retrying.") from None
