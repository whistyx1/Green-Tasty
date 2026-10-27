output "backend_url" {
  value = aws_api_gateway_stage.backend.invoke_url
}

output "frontend_bucket_name" {
  value = aws_s3_bucket.frontend.id
}

output "cognito_user_pool_id" {
  value = aws_cognito_user_pool.backend.id
}

output "cognito_client_id" {
  value = aws_cognito_user_pool_client.backend_api.id
}

output "artifacts_bucket_name" {
  value = aws_s3_bucket.artifacts.id
}
