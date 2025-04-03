terraform {
  backend "s3" {
    bucket = "framesnap-lambda-custom-tf-state"
    key    = "terraform.tfstate"
    region = "us-east-1"
  }
}

provider "aws" {
  region = var.region
}

data "aws_lambda_function" "custom_authorizer" {
  function_name = var.lambda_function_name
}

resource "aws_api_gateway_rest_api" "framesnap_api" {
  name = "FrameSnapAPI"
}

resource "aws_api_gateway_authorizer" "custom_authorizer" {
  name                              = "CustomAuthorizer"
  rest_api_id                       = aws_api_gateway_rest_api.framesnap_api.id
  authorizer_uri                    = "arn:aws:apigateway:${var.region}:lambda:path/2015-03-31/functions/${data.aws_lambda_function.custom_authorizer.arn}/invocations"
  authorizer_result_ttl_in_seconds = 300
  type                              = "REQUEST"
}

resource "aws_api_gateway_resource" "videos" {
  rest_api_id = aws_api_gateway_rest_api.framesnap_api.id
  parent_id   = aws_api_gateway_rest_api.framesnap_api.root_resource_id
  path_part   = "videos"
}

resource "aws_api_gateway_resource" "videos_proxy" {
  rest_api_id = aws_api_gateway_rest_api.framesnap_api.id
  parent_id   = aws_api_gateway_resource.videos.id
  path_part   = "{proxy+}"
}

resource "aws_api_gateway_method" "videos_proxy_method" {
  rest_api_id   = aws_api_gateway_rest_api.framesnap_api.id
  resource_id   = aws_api_gateway_resource.videos_proxy.id
  http_method   = "ANY"
  authorization = "CUSTOM"
  authorizer_id = aws_api_gateway_authorizer.custom_authorizer.id

  request_parameters = {
    "method.request.path.proxy" = true
  }
}

resource "aws_api_gateway_integration" "videos_proxy_integration" {
  rest_api_id             = aws_api_gateway_rest_api.framesnap_api.id
  resource_id             = aws_api_gateway_resource.videos_proxy.id
  http_method             = aws_api_gateway_method.videos_proxy_method.http_method
  integration_http_method = "ANY"
  uri                     = "${var.eks_service_endpoint}/{proxy}"
  type                    = "HTTP_PROXY"

  request_parameters = {
    "integration.request.path.proxy" = "method.request.path.proxy"
  }

  passthrough_behavior = "WHEN_NO_MATCH"
}

resource "aws_api_gateway_resource" "login" {
  rest_api_id = aws_api_gateway_rest_api.framesnap_api.id
  parent_id   = aws_api_gateway_rest_api.framesnap_api.root_resource_id
  path_part   = "login"
}

resource "aws_api_gateway_method" "login_method" {
  rest_api_id   = aws_api_gateway_rest_api.framesnap_api.id
  resource_id   = aws_api_gateway_resource.login.id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "login_integration" {
  rest_api_id             = aws_api_gateway_rest_api.framesnap_api.id
  resource_id             = aws_api_gateway_resource.login.id
  http_method             = aws_api_gateway_method.login_method.http_method
  integration_http_method = "POST"
  uri                     = "${var.login_service_endpoint}/login"
  type                    = "HTTP_PROXY"
  passthrough_behavior    = "WHEN_NO_MATCH"
}

resource "aws_api_gateway_deployment" "api_deployment" {
  rest_api_id = aws_api_gateway_rest_api.framesnap_api.id
  depends_on = [
    aws_api_gateway_method.videos_proxy_method,
    aws_api_gateway_method.login_method
  ]
}

resource "aws_api_gateway_stage" "prod_stage" {
  deployment_id = aws_api_gateway_deployment.api_deployment.id
  rest_api_id   = aws_api_gateway_rest_api.framesnap_api.id
  stage_name    = "prod"
}

resource "aws_lambda_permission" "allow_api_gateway" {
  statement_id  = "AllowAPIGatewayInvoke-${var.lambda_function_name}-${aws_api_gateway_rest_api.framesnap_api.id}"
  action        = "lambda:InvokeFunction"
  function_name = data.aws_lambda_function.custom_authorizer.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "arn:aws:execute-api:${var.region}:${var.account_id}:${aws_api_gateway_rest_api.framesnap_api.id}/*"
}
