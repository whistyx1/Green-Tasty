#!/usr/bin/env python3
"""Check that the Terraform migration preserves the implemented API and table keys."""
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
BACKEND = ROOT / "backend/restaurant-backend-app"
INFRA = ROOT / "infra/terraform"


def main():
    errors = []
    registry = (BACKEND / "dnapp/lambdas/api-handler/Routing/RouteRegistry.cs").read_text(encoding="utf-8-sig")
    implemented = set(re.findall(r'new Route\(HttpMethods\.(\w+), "([^"]+)"\)', registry))
    routes = json.loads((INFRA / "api-routes.json").read_text())
    deployed = {(method, path) for path, methods in routes.items() for method in methods}
    if implemented != deployed:
        errors.append(f"API mismatch: missing={sorted(implemented - deployed)}, extra={sorted(deployed - implemented)}")
    for method, path in [("POST", "/waiters"), ("DELETE", "/waiters"), ("POST", "/locations"), ("POST", "/tables"), ("POST", "/dishes")]:
        if not routes.get(path, {}).get(method):
            errors.append(f"Management operation lacks a Cognito authorizer: {method} {path}")
    for path in ["/register", "/signin", "/guest/sessions"]:
        if routes[path]["POST"]:
            errors.append(f"Public login/guest operation was incorrectly protected: {path}")

    aliases = dict(re.findall(r'^([\w-]+):\s*"([^"]*)"', (BACKEND / ".syndicate-config-restaurant-backend-app/syndicate_aliases.yml").read_text(), re.M))
    def resolve(value):
        return re.sub(r'\$\{([^}]+)\}', lambda match: aliases[match[1]], value)

    original = json.loads((BACKEND / "deployment_resources.json").read_text())
    tables = json.loads((INFRA / "tables.json").read_text())
    expected = {resolve(name): table for name, table in original.items() if table["resource_type"] == "dynamodb_table"}
    if set(tables) != set(expected):
        errors.append("DynamoDB table names differ from application metadata.")
    for name, table in expected.items():
        migrated = tables.get(name, {})
        if (migrated.get("hash_key"), migrated.get("range_key")) != (table["hash_key_name"], table.get("sort_key_name")):
            errors.append(f"Changed primary key: {name}")
        attrs = {table["hash_key_name"]: table["hash_key_type"]}
        if "sort_key_name" in table:
            attrs[table["sort_key_name"]] = table["sort_key_type"]
        indexes = {}
        for index in table["global_indexes"]:
            attrs[index["index_key_name"]] = index["index_key_type"]
            if "index_sort_key_name" in index:
                attrs[index["index_sort_key_name"]] = index["index_sort_key_type"]
            indexes[resolve(index["name"])] = {"hash_key": index["index_key_name"], "range_key": index.get("index_sort_key_name")}
        if migrated.get("indexes") != indexes or migrated.get("attributes") != attrs:
            errors.append(f"Changed index keys or attribute types: {name}")
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"Deployment contract passed: {len(deployed)} API methods, {len(tables)} tables, management authentication enforced.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
