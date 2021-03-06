﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.PlatformAbstractions;
using System.IO;
using AuthorizationServer.Configuration;
using IdentityServer4.Core.Validation;
using IdentityServer4.Core.Services;
using AuthorizationServer.Repository;
using AuthorizationServer.Models;
using Serilog;
using TwentyTwenty.IdentityServer4.EntityFramework7.Extensions;
using Microsoft.Data.Entity;
using Newtonsoft.Json;
using Microsoft.AspNet.Identity.EntityFramework;

namespace AuthorizationServer
{
    public class Startup
    {
        private readonly IApplicationEnvironment _environment;
        public static IConfigurationRoot Configuration { get; set; }

        public Startup(IHostingEnvironment env, IApplicationEnvironment environment)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true); 

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
            _environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<OperationalContext>(o => o.UseSqlServer(Startup.Configuration["Data:AuthorizationServerConnection:ConnectionString"]))
                .AddDbContext<ClientConfigurationContext>(o => o.UseSqlServer(Startup.Configuration["Data:AuthorizationServerConnection:ConnectionString"]))
                 .AddDbContext<ScopeConfigurationContext>(o => o.UseSqlServer(Startup.Configuration["Data:AuthorizationServerConnection:ConnectionString"]));

            

            #region IdentityServer4
            //var cert = new X509Certificate2(Path.Combine(_environment.ApplicationBasePath, "idsrv4test.pfx"), "idsrv3test");
            var cert = new X509Certificate2(Path.Combine(_environment.ApplicationBasePath, "DPControlSignCA.pfx"),
                "signature1234",
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

            var builder = services.AddIdentityServer(options =>
            {
                options.SigningCertificate = cert;
                options.Endpoints.EnableTokenRevocationEndpoint = true;
                options.Endpoints.EnableIdentityTokenValidationEndpoint = true;
                options.EnableWelcomePage = false;
            });


            //builder.AddInMemoryClients(Clients.Get());
            //builder.AddInMemoryScopes(Scopes.Get());
            //Configure Client and Scope
            builder.ConfigureEntityFramework()
            .RegisterOperationalStores()
            .RegisterClientStore<Guid, ClientConfigurationContext>()
            .RegisterScopeStore<Guid, ScopeConfigurationContext>();
            
            //Configure User
            builder.Services.AddTransient<IResourceOwnerPasswordValidator, ResourceOwnerPasswordValidator>();
            builder.Services.AddTransient<IProfileService, ProfileService>();
           
            builder.Services.AddTransient<ICorsPolicyService, AllowAllCorsPolicy>();
            //builder.AddCustomGrantValidator<CustomGrantValidator>();
            builder.Services.AddCoreServices();
            #endregion
            services.AddTransient<IRoleRepository, RoleRepository>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<UserDbContext, UserDbContext>();

            #region  Add Identity
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonLetterOrDigit = false;
                options.Password.RequireUppercase = false;
            })
            .AddEntityFrameworkStores<UserDbContext>()
            .AddDefaultTokenProviders();
            
            #endregion

            // for the UI
            services
                .AddMvc()
                .AddRazorOptions(razor =>
                {
                    razor.ViewLocationExpanders.Add(new AuthorizationServer.UI.CustomViewLocationExpander());
                });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(LogLevel.Verbose);
            loggerFactory.AddDebug(LogLevel.Verbose);
            var logWarning = new Serilog.LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.RollingFile(
                pathFormat: env.MapPath("Error/Exception.log"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}{NewLine}{NewLine}"
                ).CreateLogger();

            loggerFactory.AddSerilog(logWarning);


            app.UseDeveloperExceptionPage();
            app.UseIISPlatformHandler();

            app.UseIdentityServer();


            app.UseStaticFiles();
            app.UseMvcWithDefaultRoute();
        }

        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
