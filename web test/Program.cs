using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;
using web_test.Services;
using web_test;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddRazorPages(options =>
{
    // ��������� ��������� Antiforgery-�����
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});
builder.Services.AddMemoryCache(); // ��������� ����������� � ������
builder.Services.AddScoped<IWorkItemService, WorkItemService>(); // ������������ ��� ������

var app = builder.Build();
// ������������ middleware
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
