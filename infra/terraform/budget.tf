resource "aws_budgets_budget" "monthly" {
  count        = length(var.alert_emails) > 0 ? 1 : 0
  name         = "epam-restaurant-monthly"
  budget_type  = "COST"
  limit_amount = tostring(var.monthly_budget_usd)
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  # Account-wide: catches untagged costs too, including API traffic.
  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = var.alert_emails
  }
}
