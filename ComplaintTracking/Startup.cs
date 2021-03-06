﻿using ComplaintTracking.Data;
using ComplaintTracking.Models;
using ComplaintTracking.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpRaven;
using System;
using System.IO;

namespace ComplaintTracking
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;

        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env;
            Configuration = configuration;
            Setup();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add database context
            string connectionString = Configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(opts =>
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    opts.UseSqlite($"Data Source='{Path.Combine(FilePaths.BasePath, "cts-local.db")}'");
                }
                else
                {
                    opts.UseSqlServer(connectionString);
                }
            });

            // Add framework services.
            services.AddIdentity<ApplicationUser, IdentityRole>(config => config.SignIn.RequireConfirmedEmail = true)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            // Configure authorization policy
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            services.AddControllersWithViews(options => { options.Filters.Add(new AuthorizeFilter(policy)); });

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = new PathString("/Account/Login/");
                options.AccessDeniedPath = new PathString("/Account/AccessDenied/");
            });
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);

            // Data protection
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(FilePaths.DataProtectionKeysFolder));

            // Add Sentry.io error logging
            services.AddScoped<IRavenClient, RavenClient>(s =>
             {
                 return new RavenClient(Configuration["SentryDSN"])
                 {
                     Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                     Release = typeof(Program).Assembly.GetName().Version.ToString(3)
                 };
             });

            // Add sessions
            services.AddSession();

            // Add data access layer service
            services.AddScoped<DAL>();

            // Add application services
            services.AddTransient<IErrorLogger, ErrorLogger>();
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IImageService, ImageService>();
            services.AddTransient<IFileService, FileService>();

            // URL/Http Request helpers
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>()
                .AddScoped(x => x
                    .GetRequiredService<IUrlHelperFactory>()
                    .GetUrlHelper(x.GetRequiredService<IActionContextAccessor>().ActionContext));

            // Set up database
            services.AddHostedService<MigratorHostedService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                // For normal development
                app.UseDeveloperExceptionPage();

                // For testing exception handler
                // app.UseExceptionHandler("/Error");
            }
            else
            {
                app.UseHsts();
                app.UseExceptionHandler("/Error");
            }

            app.UseStatusCodePagesWithReExecute("/Error/Status/{0}");

            // Set current environment
            if (env.IsStaging())
            {
                CTS.CurrentEnvironment = ServerEnvironment.Staging;
            }
            else if (env.IsDevelopment())
            {
                CTS.CurrentEnvironment = ServerEnvironment.Development;
            }
            else
            {
                CTS.CurrentEnvironment = ServerEnvironment.Production;
            }

            app.UseStaticFiles();

            app.UseSession();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }

        private void Setup()
        {
            SetContacts();
            SetDirectories();
        }

        private void SetDirectories()
        {
            // Base path for all generated/uploaded files
            FilePaths.BasePath = string.IsNullOrWhiteSpace(Configuration["UserFilesBasePath"])
                ? "../_UserFiles" : Configuration["UserFilesBasePath"].ForceToString();

            // Set file paths
            FilePaths.AttachmentsFolder = Path.Combine(FilePaths.BasePath, "UserFiles", "attachments");
            FilePaths.DataProtectionKeysFolder = Path.Combine(FilePaths.BasePath, "cts-keys");
            FilePaths.ExportFolder = Path.Combine(FilePaths.BasePath, "DataExport");
            FilePaths.LogsFolder = Path.Combine("..", "..", "logs", "cts-logs");
            FilePaths.ThumbnailsFolder = Path.Combine(FilePaths.BasePath, "UserFiles", "thumbnails");
            FilePaths.UnsentEmailFolder = Path.Combine(FilePaths.BasePath, "UnsentEmail");

            // Create Directories
            Directory.CreateDirectory(FilePaths.AttachmentsFolder);
            Directory.CreateDirectory(FilePaths.DataProtectionKeysFolder);
            Directory.CreateDirectory(FilePaths.ExportFolder);
            Directory.CreateDirectory(FilePaths.LogsFolder);
            Directory.CreateDirectory(FilePaths.ThumbnailsFolder);
            Directory.CreateDirectory(FilePaths.UnsentEmailFolder);
        }

        private void SetContacts()
        {
            CTS.AdminEmail = Configuration["Contacts:AdminEmail"].ForceToString();
            CTS.DevEmail = Configuration["Contacts:DevEmail"].ForceToString();
            CTS.QAEmail = Configuration["Contacts:QAEmail"].ForceToString();
            CTS.AccountAdminEmail = Configuration["Contacts:AccountAdminEmail"].ForceToString();
        }
    }
}
