locals {
  lambdas = {
    api-handler = {
      handler = "APIHandler::APIHandler.Function::FunctionHandler"
      environment = {
        COGNITO_USER_POOL_ID              = aws_cognito_user_pool.backend.id
        COGNITO_USER_POOL_CLIENT_ID       = aws_cognito_user_pool_client.backend_api.id
        reports_queue_name                = aws_sqs_queue.reports.name
        OrderIndexReservationId           = "GSI_ReservationId"
        REGION                            = var.aws_region
        notificationsTable                = aws_dynamodb_table.backend["Notifications"].name
        FEEDBACK_TABLE_NAME               = aws_dynamodb_table.backend["Feedback"].name
        locationsTable                    = aws_dynamodb_table.backend["Locations"].name
        WaiterListTableName               = aws_dynamodb_table.backend["Waiters"].name
        tablesTable                       = aws_dynamodb_table.backend["Tables"].name
        dishesTable                       = aws_dynamodb_table.backend["Dishes"].name
        reservationsTable                 = aws_dynamodb_table.backend["Reservations"].name
        reportsTable                      = aws_dynamodb_table.backend["Reports"].name
        reportsHistoryTable               = aws_dynamodb_table.backend["ReportsHistory"].name
        profileImagesTable                = aws_dynamodb_table.backend["ProfileImages"].name
        notificationsUserIdIndex          = "UserIdIndex"
        reservationsCustomerIdGlobalIndex = "GSI_UserId"
        reservationsWaiterIdGlobalIndex   = "GSI_WaiterId"
        reservationsTableGlobalIndex      = "GSI_ByTableId"
        waitersGlobalIndex                = "locationId-index"
        imagesBucketName                  = aws_s3_bucket.images.id
        profileImagesBucketName           = aws_s3_bucket.profile_images.id
        ordersTable                       = aws_dynamodb_table.backend["Orders"].name
        feedbackGlobalIndex               = "locationId-createdAt-index"
        groupReservationsTable            = aws_dynamodb_table.backend["GroupReservations"].name
        guestSessionTable                 = aws_dynamodb_table.backend["GuestSessions"].name
        tableLocationIdIndex              = "locationId-tableNumber-index"
      }
    }
    reports-handler = {
      handler = "ReportsHandler::ReportsHandler.Function::FunctionHandler"
      environment = {
        ReportsTableName = aws_dynamodb_table.backend["Reports"].name
        AwsRegion        = var.aws_region
      }
    }
    report-sender = {
      handler = "ReportSender::ReportSender.Function::FunctionHandler"
      environment = {
        EmailFrom         = var.email_from
        Region            = var.aws_region
        LocationTableName = aws_dynamodb_table.backend["Locations"].name
        ReportTableName   = aws_dynamodb_table.backend["Reports"].name
        CognitoUserPoolId = aws_cognito_user_pool.backend.id
      }
    }
  }
}
