variable "aws_region" {
  description = "AWS region for all resources except CloudFront/ACM globals"
  type        = string
  default     = "us-west-2"
}

variable "account_id" {
  description = "Optional expected AWS account ID. Reject a different account when supplied; otherwise discover from the authenticated profile."
  type        = string
  default     = null

  validation {
    condition     = var.account_id == null ? true : can(regex("^[0-9]{12}$", var.account_id))
    error_message = "account_id must be a 12-digit account ID, or null."
  }
}

variable "alert_emails" {
  description = "Monthly AWS Budget notification recipients. A budget is created only when this list is non-empty."
  type        = list(string)
  default     = []
}

variable "monthly_budget_usd" {
  type        = number
  description = "Monthly account-wide notification threshold in USD; does not stop spending."
  default     = 10

  validation {
    condition     = var.monthly_budget_usd > 0
    error_message = "The budget must be positive."
  }
}

variable "email_from" {
  type        = string
  description = "SES-verified sender address in aws_region. Required before enabling weekly reports."
  default     = ""

  validation {
    condition     = var.email_from == "" || can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.email_from))
    error_message = "email_from must be an email address without surrounding whitespace, or empty while reports are disabled."
  }
}

variable "enable_weekly_reports" {
  type        = bool
  description = "Enable after SES sender/recipients and the Cognito Admin group are ready."
  default     = false
}

variable "lambda_artifact_directory" {
  type        = string
  description = "Directory relative to infra/terraform containing the three built Lambda ZIP files."
  default     = "../../artifacts/lambdas"
}
