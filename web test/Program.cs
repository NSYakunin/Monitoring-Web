using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Monitoring.Infrastructure.Services.Fake;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddMemoryCache();

// builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<IWorkItemService, FakeWorkItemService>();
// builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ILoginService, FakeLoginService>();

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var app = builder.Build();

// Если не в Dev-режиме, включаем обработку ошибок
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Razor Pages endpoints
app.MapRazorPages();

app.Run();