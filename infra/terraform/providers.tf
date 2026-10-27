terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region              = var.aws_region
  allowed_account_ids = var.account_id == null ? null : [var.account_id]

  default_tags {
    tags = {
      Project   = "epam-restaurant"
      ManagedBy = "Terraform"
    }
  }
}

# Reserved for ACM certificates once a custom domain is added (CloudFront requires
# certs to live in us-east-1 regardless of the distribution's region).
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"
}
