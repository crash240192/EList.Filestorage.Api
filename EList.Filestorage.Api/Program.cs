using EList.Api.Filtres;
using EList.Common.Constants;
using EList.Common.DI;
using EList.Common.Models;
using EList.Filestorage.Data.Linq2db.Interfaces;
using EList.Filestorage.Api.Infrastructure;
using EList.Filestorage.Api.Middleware;
using EList.Filestorage.BackgroundUploader;
using EList.Filestorage.DI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using ConfigurationManager = EList.Common.Configuration.ConfigurationManager;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
}).AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Version = "v1",
        Title = "EList.FileStorage API",
        Description = "EList.FileStorage API"
    });

    var xmlCommentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EList.Filestorage.Api.xml");
    c.IncludeXmlComments(xmlCommentsPath);

    var apiSecurityScheme = BasicAuthenticationSecuritySchemeFilter.GetOpenApiSecurityScheme();
    c.AddSecurityDefinition(apiSecurityScheme.Reference.Id, apiSecurityScheme);
    c.OperationFilter<BasicAuthenticationSecuritySchemeFilter>();
    c.OperationFilter<HeaderFilter>();
});
builder.Services.AddCors();
builder.Services.AddMvc();

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errorMessages = context.ModelState.Values.Select(x => x.Errors.Select(y => y.ErrorMessage).ToList()).ToList();
        var commandResult = CommandResult.Fail(Constants.INTERNAL_ERROR_CODE, JsonConvert.SerializeObject(errorMessages));
        var result = new BadRequestObjectResult(commandResult);
        return result;
    };
});

//builder.Services.AddHostedService(BackgroundUploaderService);

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 42949672960;
});

//var mappingConfig = new MapperConfiguration(mc => { mc.AddProfile(new AutoMapperProfile()); });
//var mapper = mappingConfig.CreateMapper();
//builder.Services.AddSingleton(mapper);

ContainerConfigurator.Configure(builder.Services, new ServiceMappingProvider());

builder.Services.AddSingleton(builder.Services);

var app = builder.Build();

var pathBase = ConfigurationManager.AppSettings["pathBase"] ?? string.Empty;
app.UsePathBase(pathBase);
app.UseStaticFiles();

app.Use((context, next) =>
{
    context.Request.EnableBuffering();
    return next();
});

app.UseHttpsRedirection();
app.UseRouting();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
    ForwardedHeaders.XForwardedProto
});

var storageDataProvider = app.Services.GetRequiredService<IFileInfoDataProvider>();
var backgroundUploader = app.Services.GetRequiredService<IBackgroundUploaderService>();

app.Lifetime.ApplicationStarted.Register(() =>
{
    backgroundUploader.Start();

    const string connectionStringName = "elist_filestorage_db";
    //versionDataProvider.Configure(connectionStringName);
    storageDataProvider.Configure(connectionStringName);
    //VersionService.Configure(versionDataProvider);
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    backgroundUploader.Stop();
});

app.MapControllers();

app.UseSwagger(c =>
{
    c.SerializeAsV2 = true;
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint($"{pathBase}/swagger/v1/swagger.json", "FileStorage API v1");
});

app.Run();
