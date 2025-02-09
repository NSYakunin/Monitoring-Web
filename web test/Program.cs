using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;
using web_test.Services;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddTransient<IWorkItemService, WorkItemService>();

builder.Services.AddRazorPages(options =>
{
    // отключаем глобально Antiforgery-токен
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});

var app = builder.Build();


app.MapRazorPages();

app.Run();
