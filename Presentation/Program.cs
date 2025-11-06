using Amazon;
using Amazon.SimpleEmail;
using Data.Access.Booking;
using Data.Access.Connection;
using Data.Access.Customer;
using Data.Business.Booking;
using Data.Business.Customer;
using Data.Business.Data;
using Data.Business.Service.Hubs;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using OSV.Areas.Identity;
using OSV.Data;
using Log = Serilog.Log;
using Data.Business.Service.Notification;
using System.Text;
using Data.Business.Service;
using FluentValidation;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
Directory.CreateDirectory(logDirectory); // <-- السطر ده هيضمن إن الفولدر موجود
var logPath = Path.Combine(logDirectory, "log-.txt");

// Bootstrap Logger قبل إنشاء الـ Builder
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        shared: true, // <--- 💡 ضيف السطر ده هنا
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // استخدام Serilog مع Host
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            shared: true,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        )
    );

    // (1) <--- إضافة
    // ------------------------------------
    // لازم نضيف ده عشان الـ API Controllers تشتغل
    builder.Services.AddControllers();
    // ------------------------------------
    Log.Debug("Application Starting Up");
    builder.Services.AddRazorPages();

    builder.Services.AddFluentValidationAutoValidation(options =>
    {
        options.DisableDataAnnotationsValidation = false;
    });

    builder.Services.AddFluentValidationClientsideAdapters();
    builder.Services.AddValidatorsFromAssemblyContaining<CustomerValidator>();

    // --- DbContext للـ Identity ---
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // --- خدمات الـ Identity ---
    builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    });
    // Configure file upload limits for large Excel imports
    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 52428800; // 50 MB
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartHeadersLengthLimit = int.MaxValue;
    });
    builder.Services.Configure<AwsSesSettings>(
        builder.Configuration.GetSection("AwsSesSettings")
    );
    builder.Services.AddSingleton<IAmazonSimpleEmailService>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>().GetSection("AwsSesSettings").Get<AwsSesSettings>();

        if (config == null)
            throw new InvalidOperationException("AwsSesSettings section is missing or invalid in configuration.");

        if (string.IsNullOrWhiteSpace(config.Region))
            throw new InvalidOperationException("AwsSesSettings.Region is not configured.");

        var awsConfig = new AmazonSimpleEmailServiceConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region)
        };

        return new AmazonSimpleEmailServiceClient(config.AccessKeyId, config.SecretAccessKey, awsConfig);
    });

    builder.Services.AddScoped<IEmailSender, AwsSesEmailService>();
    // Connection Factory (Dapper)
    builder.Services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
    // Repositories (Dapper)
    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
    builder.Services.AddScoped<IBookingRepository, BookingRepository>();
    // Services
    builder.Services.AddScoped<OSV.Attributes.CalComSignatureAuthFilter>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IBookingService, BookingService>();
    builder.Services.AddHttpClient("CalClient", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["CalCom:BaseUrl"] ?? "https://api.cal.com/");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                builder.Configuration["CalCom:ApiKey"]
            );
        client.DefaultRequestHeaders.Add("cal-api-version", "2024-08-13");
    });
    // HttpClientFactory (Retell)
    builder.Services.AddHttpClient("RetellClient", (serviceProvider, client) =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var apiKey = configuration["Retell:ApiKey"];

        // --- (التصحيح النهائي) ---
        // (1) استخدام الدومين الصحيح
        client.BaseAddress = new Uri("https://api.retellai.com/");

        //// (اختياري: يمكنك جعل الكود يقرأ من الإعدادات كـ fallback)
        //// var baseUrlFromConfig = configuration["Retell:BaseUrl"];
        //// var correctBaseUrl = new Uri(baseUrlFromConfig ?? "https://api.retellai.com/");

        //client.BaseAddress = correctBaseUrl;
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

        // (2) إضافة User-Agent (لضمان تجاوز أي WAF/Cloudflare)
        client.DefaultRequestHeaders.UserAgent.ParseAdd("My-OSV-App/1.0.0");
    });
 
    // SignalR
    builder.Services.AddSignalR();
    var app = builder.Build();
    // Configure the HTTP request pipeline
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
 
    app.UseHttpsRedirection();
 
    app.UseStaticFiles();
  
    app.UseRouting();
  
    // ------------------------------------
    // (2) <--- إضافة
    // ------------------------------------
    app.Use(async (context, next) => {
        context.Request.EnableBuffering();
        await next();
    });
    // ------------------------------------

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");


    app.MapControllers();
    // -----------------------------------
    app.MapRazorPages();
    app.MapHub<HubNotification>("/customerHub");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
}
finally
{
    Log.CloseAndFlush();
}
