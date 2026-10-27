#!/usr/bin/env bash
# Creates/configures ONLY the remote-state bucket. Run explicitly from your terminal.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
region="${AWS_REGION:-us-west-2}"
account_id="$(aws sts get-caller-identity --region "$region" --query Account --output text)"
bucket="restaurant-terraform-state-${account_id}"
error_file="$(mktemp)"
trap 'rm -f "$error_file"' EXIT

if ! aws s3api head-bucket --bucket "$bucket" --expected-bucket-owner "$account_id" --region "$region" 2>"$error_file"; then
  if ! grep -Eq '\(404\)|Not Found|NotFound' "$error_file"; then
    cat "$error_file" >&2
    exit 1
  fi
  if [[ "$region" == "us-east-1" ]]; then
    aws s3api create-bucket --bucket "$bucket" --region "$region" >/dev/null
  else
    aws s3api create-bucket --bucket "$bucket" --region "$region" \
      --create-bucket-configuration "LocationConstraint=$region" >/dev/null
  fi
fi

aws s3api put-bucket-versioning --bucket "$bucket" --region "$region" \
  --versioning-configuration Status=Enabled
aws s3api put-public-access-block --bucket "$bucket" --region "$region" \
  --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
aws s3api put-bucket-encryption --bucket "$bucket" --region "$region" \
  --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'

umask 077
printf 'bucket = "%s"\nregion = "%s"\n' "$bucket" "$region" > "$repo_root/infra/terraform/backend.local.hcl"
printf 'State bucket ready. Local backend configuration saved without displaying your account ID.\n'
printf 'Next: terraform -chdir=infra/terraform init -upgrade -backend-config=backend.local.hcl\n'
