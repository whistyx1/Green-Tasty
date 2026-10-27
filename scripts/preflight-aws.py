#!/usr/bin/env python3
"""Read-only account and name-collision checks. Never prints account IDs or ARNs."""
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    region = sys.argv[1] if len(sys.argv) > 1 else "us-west-2"

    def aws(*args):
        result = subprocess.run([
            "aws", *args, "--region", region, "--output", "json", "--no-cli-pager",
            "--cli-connect-timeout", "10", "--cli-read-timeout", "20",
        ], capture_output=True, text=True)
        if result.returncode:
            raise SystemExit(f"Read-only AWS check failed for {args[0]} {args[1]}. Check connectivity and profile permissions in your terminal.")
        output = result.stdout.strip()
        # SQS ListQueues can succeed without a response body when no queues match.
        if not output and args[:2] == ("sqs", "list-queues"):
            return {}
        try:
            response = json.loads(output)
        except json.JSONDecodeError:
            raise SystemExit(f"AWS returned an invalid JSON response for {args[0]} {args[1]}. Inventory is incomplete; do not deploy yet.") from None
        if not isinstance(response, dict):
            raise SystemExit(f"AWS returned an unexpected response for {args[0]} {args[1]}. Inventory is incomplete; do not deploy yet.")
        return response

    account = aws("sts", "get-caller-identity")["Account"]
    print(f"AWS authentication OK; account ID hidden. Region: {region}.")
    collisions = []
    desired_tables = set(json.loads((ROOT / "infra/terraform/tables.json").read_text()))
    desired_lambdas = {"api-handler", "reports-handler", "report-sender"}
    desired_buckets = {
        f"restaurant-backend-dev-{kind}-{account}" for kind in ["images", "profile-images", "swagger-ui"]
    } | {f"restaurant-frontend-{account}", f"marathon2026-restaurant-artifacts-{account}"}

    for table in aws("dynamodb", "list-tables")["TableNames"]:
        if table in desired_tables:
            collisions.append(f"DynamoDB: {table}")
    for function in aws("lambda", "list-functions")["Functions"]:
        if function["FunctionName"] in desired_lambdas:
            collisions.append(f"Lambda: {function['FunctionName']}")
    for bucket in aws("s3api", "list-buckets")["Buckets"]:
        if bucket["Name"] in desired_buckets:
            collisions.append("S3: " + bucket["Name"].replace(account, "<account>"))
    for role in aws("iam", "list-roles")["Roles"]:
        if role["RoleName"] in {f"{name}-role" for name in desired_lambdas} | {"event-bridge-role"}:
            collisions.append(f"IAM role: {role['RoleName']}")
    for policy in aws("iam", "list-policies", "--scope", "Local")["Policies"]:
        if policy["PolicyName"] == "event-bridge-policy":
            collisions.append("IAM policy: event-bridge-policy")
    for api in aws("apigateway", "get-rest-apis")["items"]:
        if api["name"] == "restaurant-backend-api":
            collisions.append("API Gateway: restaurant-backend-api")
    for pool in aws("cognito-idp", "list-user-pools", "--max-results", "60")["UserPools"]:
        if pool["Name"] == "restaurant-backend-user-pool":
            collisions.append("Cognito: restaurant-backend-user-pool")
    for queue in aws("sqs", "list-queues", "--queue-name-prefix", "restaurant-reports-queue").get("QueueUrls", []):
        collisions.append("SQS: " + queue.rsplit("/", 1)[-1])
    for schedule in aws("scheduler", "list-schedules", "--name-prefix", "weekly-reports-schedule").get("Schedules", []):
        if schedule["Name"] == "weekly-reports-schedule":
            collisions.append("Scheduler: weekly-reports-schedule")
    for group in aws("logs", "describe-log-groups", "--log-group-name-prefix", "/aws/lambda/")["logGroups"]:
        if group["logGroupName"].removeprefix("/aws/lambda/") in desired_lambdas:
            collisions.append("CloudWatch log group: " + group["logGroupName"])
    print("Read-only resource inventory finished. No resources were changed.")
    if collisions:
        print("Existing names require review/import before the first apply:")
        print("\n".join("  " + item for item in collisions))
        return 1
    print("No name collisions found among the regional/account resources checked.")
    print("Also check CloudFront OAC name restaurant-frontend-oac and budget epam-restaurant-monthly if previously created.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
