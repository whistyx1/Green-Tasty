# Creating an email identity sends an AWS verification link to this address.
# Apply with weekly reports disabled, verify the address, then enable the schedule.
resource "aws_ses_email_identity" "reports" {
  count = trimspace(var.email_from) != "" ? 1 : 0
  email = trimspace(var.email_from)
}
