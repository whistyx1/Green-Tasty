resource "aws_iam_role" "lambda" {
  for_each = local.lambdas
  name     = "${each.key}-role"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Action    = "sts:AssumeRole"
      Principal = { Service = "lambda.amazonaws.com" }
    }]
  })
}

locals {
  table_arns = flatten([for table in aws_dynamodb_table.backend : [table.arn, "${table.arn}/index/*"]])
  lambda_statements = {
    api-handler = [
      {
        Effect   = "Allow"
        Action   = ["dynamodb:Scan", "dynamodb:GetItem", "dynamodb:Query", "dynamodb:PutItem", "dynamodb:BatchGetItem", "dynamodb:BatchWriteItem", "dynamodb:DeleteItem", "dynamodb:UpdateItem", "dynamodb:DescribeTable"]
        Resource = local.table_arns
      },
      {
        Effect   = "Allow"
        Action   = ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"]
        Resource = ["${aws_s3_bucket.images.arn}/*", "${aws_s3_bucket.profile_images.arn}/*"]
      },
      {
        Effect   = "Allow"
        Action   = ["cognito-idp:AdminConfirmSignUp", "cognito-idp:AdminAddUserToGroup", "cognito-idp:CreateGroup", "cognito-idp:GetGroup", "cognito-idp:AdminRemoveUserFromGroup", "cognito-idp:ListGroups", "cognito-idp:ListUsersInGroup", "cognito-idp:AdminUpdateUserAttributes", "cognito-idp:ListUsers", "cognito-idp:AdminGetUser", "cognito-idp:AdminListGroupsForUser"]
        Resource = [aws_cognito_user_pool.backend.arn]
      },
      {
        Effect   = "Allow"
        Action   = ["sqs:SendMessage", "sqs:GetQueueUrl"]
        Resource = [aws_sqs_queue.reports.arn]
      }
    ]
    reports-handler = [
      {
        Effect   = "Allow"
        Action   = ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:UpdateItem", "dynamodb:DescribeTable"]
        Resource = [aws_dynamodb_table.backend["Reports"].arn]
      },
      {
        Effect   = "Allow"
        Action   = ["sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:ChangeMessageVisibility", "sqs:GetQueueAttributes"]
        Resource = [aws_sqs_queue.reports.arn]
      }
    ]
    report-sender = [
      {
        Effect   = "Allow"
        Action   = ["dynamodb:Scan", "dynamodb:Query", "dynamodb:GetItem", "dynamodb:DescribeTable"]
        Resource = [aws_dynamodb_table.backend["Reports"].arn, aws_dynamodb_table.backend["Locations"].arn]
      },
      {
        Effect   = "Allow"
        Action   = ["cognito-idp:ListUsersInGroup"]
        Resource = [aws_cognito_user_pool.backend.arn]
      },
      {
        Effect   = "Allow"
        Action   = ["ses:SendRawEmail"]
        Resource = trimspace(var.email_from) != "" ? [aws_ses_email_identity.reports[0].arn] : ["arn:aws:ses:${var.aws_region}:${local.account_id}:identity/*"]
      }
    ]
  }
}

resource "aws_iam_role_policy" "lambda" {
  for_each = local.lambdas
  name     = "${each.key}-permissions"
  role     = aws_iam_role.lambda[each.key].id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = concat([
      {
        Effect   = "Allow"
        Action   = ["logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = ["${aws_cloudwatch_log_group.lambda[each.key].arn}:*"]
      }
    ], local.lambda_statements[each.key])
  })
}
