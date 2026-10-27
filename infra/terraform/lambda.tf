resource "aws_cloudwatch_log_group" "lambda" {
  for_each          = local.lambdas
  name              = "/aws/lambda/${each.key}"
  retention_in_days = 30
}

resource "aws_s3_object" "lambda_package" {
  for_each    = local.lambdas
  bucket      = aws_s3_bucket.artifacts.id
  key         = "lambdas/${each.key}/${filebase64sha256("${local.lambda_artifact_path}/${each.key}.zip")}.zip"
  source      = "${local.lambda_artifact_path}/${each.key}.zip"
  source_hash = filebase64sha256("${local.lambda_artifact_path}/${each.key}.zip")

  depends_on = [aws_s3_bucket_server_side_encryption_configuration.artifacts]
}

resource "aws_lambda_function" "backend" {
  for_each         = local.lambdas
  function_name    = each.key
  role             = aws_iam_role.lambda[each.key].arn
  handler          = each.value.handler
  runtime          = "dotnet10"
  architectures    = ["x86_64"]
  memory_size      = 512
  timeout          = 100
  publish          = true
  s3_bucket        = aws_s3_object.lambda_package[each.key].bucket
  s3_key           = aws_s3_object.lambda_package[each.key].key
  source_code_hash = filebase64sha256("${local.lambda_artifact_path}/${each.key}.zip")

  environment {
    variables = each.value.environment
  }

  depends_on = [aws_cloudwatch_log_group.lambda, aws_iam_role_policy.lambda]
}

resource "aws_lambda_alias" "backend" {
  for_each         = local.lambdas
  name             = local.lambda_alias
  function_name    = aws_lambda_function.backend[each.key].function_name
  function_version = aws_lambda_function.backend[each.key].version
}

resource "aws_lambda_event_source_mapping" "reports" {
  event_source_arn        = aws_sqs_queue.reports.arn
  function_name           = aws_lambda_alias.backend["reports-handler"].arn
  batch_size              = 10
  function_response_types = ["ReportBatchItemFailures"]
}
