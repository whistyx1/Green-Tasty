#!/usr/bin/env python3
"""Manually check level two using local prompts; --submit sends the group order.

Credentials and tokens stay in memory. IDs are prompted rather than placed in
shell history. By default, only the stable owner session is ensured.
--invite-link retrieves the selected booking's existing invitation.
"""
import argparse
from getpass import getpass
import json
from pathlib import Path
import runpy
import subprocess
import sys
from urllib.parse import quote

_helper = runpy.run_path(str(Path(__file__).with_name("check-group-sessions.py")))
ROOT, request = _helper["ROOT"], _helper["request"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group()
    action.add_argument("--submit", action="store_true", help="Submit the selected group preorder to the restaurant.")
    action.add_argument("--invite-link", action="store_true", help="Retrieve the existing invite link for your booking.")
    parser.add_argument("--anonymous", action="store_true", help="Use no login token; expect HTTP 401.")
    args = parser.parse_args()
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
            raise SystemExit(f"Sign-in failed: HTTP {status}.")
        token = login["idToken"]
        del login
    reservation_id = input("Reservation ID (kept local): ").strip()
    if not reservation_id:
        raise SystemExit("A reservation ID is required.")
    if args.invite_link:
        # Check ownership before calling the legacy invite-token endpoint.
        status, response = request(base_url, "GET", "/reservations/" + quote(reservation_id, safe="") + "/guest-session-id", token=token)
        if status == 200:
            status, response = request(base_url, "GET", "/reservations/" + quote(reservation_id, safe="") + "/invite-token", token=token)
        print(f"Invite lookup: HTTP {status}")
        if status != 200:
            print(json.dumps(response, indent=2, ensure_ascii=False))
            return 1
        invitation = response.get("token") or response.get("Token")
        if not isinstance(invitation, str) or not invitation:
            raise SystemExit("The invite response did not contain a token.")
        frontend = subprocess.run([
            "terraform", f"-chdir={ROOT / 'infra/terraform'}", "output", "-raw", "cloudfront_distribution_domain_name",
        ], capture_output=True, text=True)
        domain = frontend.stdout.strip()
        if frontend.returncode or not domain.endswith(".cloudfront.net") or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789.-" for c in domain):
            raise SystemExit("Could not read the deployed CloudFront domain from Terraform.")
        print("Open this existing invitation in a private window:")
        print("https://" + domain + "/join/" + quote(invitation, safe=""))
        print("Keep this invitation local; do not paste it into chat.")
        return 0
    elif args.submit:
        group_id = input("Group reservation ID (kept local): ").strip()
        if not group_id:
            raise SystemExit("A group reservation ID is required.")
        print("This sends the group preorder and locks further guest cart changes on success.")
        status, response = request(base_url, "POST", "/group-reservations/" + quote(group_id, safe="") + "/submit",
                                   {"reservationId": reservation_id}, token=token)
        label = "Group-submit endpoint"
    else:
        status, response = request(base_url, "GET", "/reservations/" + quote(reservation_id, safe="") + "/guest-session-id", token=token)
        label = "Owner-session endpoint"
    print(f"{label}: HTTP {status}")
    print(json.dumps(response, indent=2, ensure_ascii=False))
    print("Keep IDs, tokens, and any private data local; share only status and test quantities.")
    return 0 if status == 200 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        raise SystemExit("\nStopped. A request already sent may have completed; check its state before retrying.") from None
