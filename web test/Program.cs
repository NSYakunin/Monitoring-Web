using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;
using web_test.Services;
using web_test;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddRazorPages(options =>
{
    // отключаем глобально Antiforgery-токен
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});
builder.Services.AddMemoryCache(); // Добавляем кеширование в память
builder.Services.AddScoped<IWorkItemService, WorkItemService>(); // Регистрируем наш сервис

var app = builder.Build();
// Конфигурация middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
