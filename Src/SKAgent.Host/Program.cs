using Microsoft.AspNetCore.HttpLogging;
using Scalar.AspNetCore;
using SKAgent.Core.Tools.Abstractions;
using SKAgent.Host;
using SKAgent.Host.Boostrap;

// ============================================
// 【Host 层 - 应用程序入口】
// ASP.NET Core Web API 宿主，配置服务和 HTTP 管道。
// ============================================

var builder = WebApplication.CreateBuilder(args);
var replayUiCorsPolicy = "ReplayUiCors";

// 1. 注册 MVC 控制器服务
builder.Services.AddControllers();

// 1.1 注册 CORS，允许独立 Replay UI 在本地开发时直连 Host API。
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(replayUiCorsPolicy, policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// 2. 注册所有 SKAgent 相关服务（Kernel、Agent、Runtime、Memory、Profile 等）
builder.Services.AddSkAgentServices(builder.Configuration);

// 3. 注册 OpenAPI（Swagger）支持
builder.Services.AddOpenApi();

// 4. 注册 HTTP 日志（记录请求方法、路径和响应状态码）
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath | HttpLoggingFields.ResponseStatusCode;
});

var app = builder.Build();

// 5. 开发环境下启用 OpenAPI 和 Scalar API 文档界面
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


// ✅ Week5-1: Tool registry bootstrap (register built-in tools once at startup)
using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<IToolBootstrapper>();
    bootstrapper.RegisterAll(registry);
}

// 6. 强制 HTTPS 重定向
app.UseHttpsRedirection();

// 7. 启用 HTTP 日志中间件
app.UseHttpLogging();

// 8. 启用 CORS，支持前端独立开发服务器或直连模式。
app.UseCors(replayUiCorsPolicy);

// 9. 启用授权中间件
app.UseAuthorization();

// 10. 映射控制器路由
app.MapControllers();

app.Run();
