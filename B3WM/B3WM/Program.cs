using B3WM.Client.Pages;
using B3WM.Client.Services;
using B3WM.Components;
using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Model;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using MudBlazor.Services;

namespace B3WM
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = long.MaxValue;
            });

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue;
            });


            // Only enable WebAssembly interactive components (client-side rendering)
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            // Add services to the container.
            builder.Services.AddMudServices();

            builder.Services.AddCors(options => 
            {   
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });

            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.IgnoreReadOnlyProperties = true;
                options.JsonSerializerOptions.IgnoreReadOnlyFields = true;
            });


            builder.Services.AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 1024 * 1024;
                options.StreamBufferCapacity = 100;
            });

            builder.Services.AddResponseCompression(opts =>
            {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    ["application/octet-stream"]);
            });

            //fazer aqui uma lista de timeframes que gostaria de gerar, e criar um serviço singleton para cada um, passando o timeframe como parametro - FUTURO

            //serviços uteis
            builder.Services.AddSingleton(sp => new CandleService(2, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>()));
            builder.Services.AddSingleton(sp => new CandleService(5, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>()));
            builder.Services.AddSingleton(sp => new BubbleService(sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>()));
            builder.Services.AddSingleton(sp => new VolumeService(sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>()));

            builder.Services.AddSingleton<OrchestratorService>();
            builder.Services.AddSingleton<TickChannelService>();
            builder.Services.AddHostedService<TickProcessorService>();

            var app = builder.Build();

            app.UseResponseCompression();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseCors();

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();


            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Home).Assembly);

            app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), api =>
            {
                api.UseRouting();
                api.UseAuthorization();
                api.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    app.MapHub<DataHub>("/api/datahub");
                });
            });

            

#if DEBUG
            builder.WebHost.UseUrls("https://localhost:5002",
                "https://0.0.0.0:5002");
#endif

            app.Map("/null", () => DateTime.Now.ToString());

            app.Run();
        }
    }
}
