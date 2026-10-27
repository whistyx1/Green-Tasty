locals {
  # true = Cognito ID token required. Guest bearer tokens are validated in Lambda.
  api_routes = {
    # Gateway allows only one variable child per parent, with a consistent name.
    # Lambda matches the real URL and supplies its own route parameter names.
    for path, methods in jsondecode(file("${path.module}/api-routes.json")) :
    replace(path, "/locations/{locationId}", "/locations/{id}") => methods
  }
  api_definition = {
    openapi = "3.0.1"
    info    = { title = local.api_gateway_name, version = "1.0" }
    components = {
      securitySchemes = {
        cognito = {
          type                           = "apiKey"
          name                           = "Authorization"
          in                             = "header"
          "x-amazon-apigateway-authtype" = "cognito_user_pools"
          "x-amazon-apigateway-authorizer" = {
            type         = "cognito_user_pools"
            providerARNs = [aws_cognito_user_pool.backend.arn]
          }
        }
      }
    }
    paths = {
      for path, methods in local.api_routes : path => merge({
        for method, authenticated in methods : lower(method) => {
          parameters = [for match in regexall("\\{([^}]+)\\}", path) : {
            name     = match[0]
            in       = "path"
            required = true
            schema   = { type = "string" }
          }]
          security  = authenticated ? [{ cognito = [] }] : []
          responses = { "200" = { description = "Lambda proxy response" } }
          "x-amazon-apigateway-integration" = {
            type            = "aws_proxy"
            httpMethod      = "POST"
            uri             = aws_lambda_alias.backend["api-handler"].invoke_arn
            timeoutInMillis = 29000
          }
        }
        }, {
        options = {
          security = []
          responses = {
            "200" = {
              description = "CORS preflight"
              headers = {
                for header in ["Access-Control-Allow-Origin", "Access-Control-Allow-Methods", "Access-Control-Allow-Headers"] :
                header => { schema = { type = "string" } }
              }
            }
          }
          "x-amazon-apigateway-integration" = {
            type             = "mock"
            requestTemplates = { "application/json" = "{\"statusCode\":200}" }
            responses = {
              default = {
                statusCode = "200"
                responseParameters = {
                  "method.response.header.Access-Control-Allow-Origin"  = "'*'"
                  "method.response.header.Access-Control-Allow-Methods" = "'GET,POST,PUT,PATCH,DELETE,OPTIONS'"
                  "method.response.header.Access-Control-Allow-Headers" = "'Content-Type,Authorization'"
                }
              }
            }
          }
        }
      })
    }
    "x-amazon-apigateway-gateway-responses" = {
      for kind in ["DEFAULT_4XX", "DEFAULT_5XX"] : kind => {
        responseParameters = {
          "gatewayresponse.header.Access-Control-Allow-Origin" = "'*'"
        }
      }
    }
  }
}

resource "aws_api_gateway_rest_api" "backend" {
  name               = local.api_gateway_name
  body               = jsonencode(local.api_definition)
  put_rest_api_mode  = "overwrite"
  binary_media_types = ["application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]

  endpoint_configuration {
    types = ["REGIONAL"]
  }
}

resource "aws_lambda_permission" "api_gateway" {
  statement_id  = "AllowRestaurantApiGateway"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.backend["api-handler"].function_name
  qualifier     = aws_lambda_alias.backend["api-handler"].name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.backend.execution_arn}/*/*"
}

resource "aws_api_gateway_deployment" "backend" {
  rest_api_id = aws_api_gateway_rest_api.backend.id
  triggers = {
    redeployment = sha1(jsonencode(local.api_definition))
  }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_api_gateway_stage" "backend" {
  rest_api_id   = aws_api_gateway_rest_api.backend.id
  deployment_id = aws_api_gateway_deployment.backend.id
  stage_name    = local.stage_name

  depends_on = [aws_lambda_permission.api_gateway]
}
