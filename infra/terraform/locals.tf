data "aws_caller_identity" "current" {}

locals {
  account_id               = data.aws_caller_identity.current.account_id
  api_gateway_name         = "restaurant-backend-api"
  stage_name               = "develop"
  lambda_alias             = "dev"
  lambda_artifact_path     = abspath("${path.module}/${var.lambda_artifact_directory}")
  report_sender_lambda_arn = aws_lambda_alias.backend["report-sender"].arn
}
