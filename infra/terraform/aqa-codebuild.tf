variable "enable_aqa_codebuild" {
  type        = bool
  description = "Create a manual CodeBuild AQA job; disabled until the AQA source and private secret are ready."
  default     = false
}

variable "aqa_ci_accounts_secret_arn" {
  type        = string
  description = "ARN of a pre-created Secrets Manager JSON secret containing the AQA account usernames and passwords. Never put its value in Terraform."
  default     = null
  nullable    = true
}

variable "aqa_reportportal_secret_arn" {
  type        = string
  description = "Optional ARN of a pre-created Secrets Manager JSON secret with REPORTPORTAL_API_KEY."
  default     = null
  nullable    = true
}

variable "aqa_reportportal_url" {
  type        = string
  description = "Optional externally reachable ReportPortal server URL."
  default     = ""
}

variable "aqa_reportportal_project" {
  type        = string
  description = "Optional ReportPortal project name."
  default     = ""
}

locals {
  aqa_account_secret_keys = toset([
    "OWNER_USERNAME", "OWNER_PASSWORD",
    "OTHER_CUSTOMER_USERNAME", "OTHER_CUSTOMER_PASSWORD",
    "WAITER_USERNAME", "WAITER_PASSWORD",
    "ADMIN_USERNAME", "ADMIN_PASSWORD",
  ])
}

resource "aws_s3_object" "aqa_source" {
  count                  = var.enable_aqa_codebuild ? 1 : 0
  bucket                 = aws_s3_bucket.artifacts.id
  key                    = "aqa-source/current.zip"
  source                 = "${path.module}/../../.build/aqa-source.zip"
  source_hash            = filemd5("${path.module}/../../.build/aqa-source.zip")
  server_side_encryption = "AES256"

  depends_on = [aws_s3_bucket_server_side_encryption_configuration.artifacts]
}

resource "aws_s3_bucket_lifecycle_configuration" "aqa_results" {
  count  = var.enable_aqa_codebuild ? 1 : 0
  bucket = aws_s3_bucket.artifacts.id

  rule {
    id     = "expire-aqa-results"
    status = "Enabled"

    filter {
      prefix = "aqa/"
    }

    expiration {
      days = 7
    }
  }
}

resource "aws_cloudwatch_log_group" "aqa_codebuild" {
  count             = var.enable_aqa_codebuild ? 1 : 0
  name              = "/aws/codebuild/epam-restaurant-aqa"
  retention_in_days = 7
}

resource "aws_iam_role" "aqa_codebuild" {
  count = var.enable_aqa_codebuild ? 1 : 0
  name  = "epam-restaurant-aqa-codebuild"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "codebuild.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy" "aqa_codebuild" {
  count = var.enable_aqa_codebuild ? 1 : 0
  name  = "epam-restaurant-aqa-codebuild"
  role  = aws_iam_role.aqa_codebuild[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = "${aws_cloudwatch_log_group.aqa_codebuild[0].arn}:*"
      },
      {
        Effect   = "Allow"
        Action   = ["s3:GetBucketLocation"]
        Resource = [aws_s3_bucket.artifacts.arn]
      },
      {
        Effect   = "Allow"
        Action   = ["s3:GetObject"]
        Resource = [aws_s3_object.aqa_source[0].arn]
      },
      {
        Effect   = "Allow"
        Action   = ["s3:PutObject"]
        Resource = ["${aws_s3_bucket.artifacts.arn}/aqa/*"]
      },
      {
        Effect   = "Allow"
        Action   = ["secretsmanager:GetSecretValue"]
        Resource = compact([var.aqa_ci_accounts_secret_arn, var.aqa_reportportal_secret_arn])
      }
    ]
  })
}

resource "aws_codebuild_project" "aqa" {
  count         = var.enable_aqa_codebuild ? 1 : 0
  name          = "epam-restaurant-aqa"
  description   = "Manual EPAM AQA live tests against the deployed Green-Tasty app"
  service_role  = aws_iam_role.aqa_codebuild[0].arn
  build_timeout = 30
  source {
    type      = "S3"
    location  = "${aws_s3_bucket.artifacts.bucket}/${aws_s3_object.aqa_source[0].key}"
    buildspec = "aqa/buildspec.yml"
  }

  artifacts {
    type                = "S3"
    location            = aws_s3_bucket.artifacts.bucket
    path                = "aqa"
    name                = "results"
    namespace_type      = "BUILD_ID"
    packaging           = "ZIP"
    encryption_disabled = false
  }

  environment {
    compute_type                = "BUILD_GENERAL1_MEDIUM"
    image                       = "aws/codebuild/standard:7.0"
    type                        = "LINUX_CONTAINER"
    image_pull_credentials_type = "CODEBUILD"

    environment_variable {
      name  = "AQA_LEVEL"
      value = "1"
    }
    environment_variable {
      name  = "UI_URL"
      value = "https://${aws_cloudfront_distribution.frontend.domain_name}"
    }
    environment_variable {
      name  = "API_URL"
      value = aws_api_gateway_stage.backend.invoke_url
    }
    environment_variable {
      name  = "HEADLESS"
      value = "true"
    }
    environment_variable {
      name  = "REPORTPORTAL_ENABLED"
      value = var.aqa_reportportal_secret_arn == null ? "false" : "true"
    }

    dynamic "environment_variable" {
      for_each = local.aqa_account_secret_keys
      content {
        name  = environment_variable.value
        type  = "SECRETS_MANAGER"
        value = "${var.aqa_ci_accounts_secret_arn}:${environment_variable.value}"
      }
    }

    dynamic "environment_variable" {
      for_each = var.aqa_reportportal_secret_arn == null ? [] : [var.aqa_reportportal_secret_arn]
      content {
        name  = "REPORTPORTAL_API_KEY"
        type  = "SECRETS_MANAGER"
        value = "${environment_variable.value}:REPORTPORTAL_API_KEY"
      }
    }
    environment_variable {
      name  = "REPORTPORTAL_URL"
      value = var.aqa_reportportal_url
    }
    environment_variable {
      name  = "REPORTPORTAL_PROJECT"
      value = var.aqa_reportportal_project
    }
  }

  logs_config {
    cloudwatch_logs {
      group_name = aws_cloudwatch_log_group.aqa_codebuild[0].name
    }
  }

  lifecycle {
    precondition {
      condition     = var.aqa_ci_accounts_secret_arn != null && var.aqa_ci_accounts_secret_arn != ""
      error_message = "Set aqa_ci_accounts_secret_arn to a pre-created JSON secret before enabling CodeBuild."
    }
    precondition {
      condition     = var.aqa_reportportal_secret_arn == null || (var.aqa_reportportal_url != "" && var.aqa_reportportal_project != "")
      error_message = "A ReportPortal secret also requires aqa_reportportal_url and aqa_reportportal_project."
    }
  }

  depends_on = [aws_iam_role_policy.aqa_codebuild]
}

output "aqa_codebuild_project_name" {
  value = var.enable_aqa_codebuild ? aws_codebuild_project.aqa[0].name : null
}
