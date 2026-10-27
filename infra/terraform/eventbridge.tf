resource "aws_scheduler_schedule" "weekly_reports" {
  name        = "weekly-reports-schedule"
  group_name  = "default"
  description = "Weekly reports generation"
  state       = var.enable_weekly_reports ? "ENABLED" : "DISABLED"

  schedule_expression          = "cron(0 8 ? * FRI *)"
  schedule_expression_timezone = "UTC"

  flexible_time_window {
    mode = "OFF"
  }

  target {
    arn      = local.report_sender_lambda_arn
    role_arn = aws_iam_role.event_bridge.arn

    retry_policy {
      maximum_event_age_in_seconds = 3600
      maximum_retry_attempts       = 3
    }
  }
  lifecycle {
    precondition {
      condition     = !var.enable_weekly_reports || can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.email_from))
      error_message = "Provide an SES-verified email_from before enabling weekly reports."
    }
  }

  depends_on = [aws_iam_role_policy_attachment.event_bridge]
}
