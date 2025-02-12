// Monitoring.UI/Program.cs
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 1) Подключаем RazorPages
builder.Services.AddRazorPages();

// 2) Подключаем MemoryCache (если используешь)
builder.Services.AddMemoryCache();

// 3) Регистрируем наши сервисы из Infrastructure
//    Связываем интерфейс -> реализация
builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<ILoginService, LoginService>();

// 4) Конфигурация подключения к БД (если appsettings.json, то вот так)
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Или можешь настроить явно в appsettings.Development.json и т.д.

// Если хочешь, можешь напрямую прописать:
// builder.Services.Configure<...>(...) // Или просто хранить в builder.Configuration

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