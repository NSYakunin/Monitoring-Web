using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ��������� � ���������� ������� Razor Pages
builder.Services.AddRazorPages(options =>
{
    // ��������� ��������� Antiforgery-�����
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});

var app = builder.Build();

// ��������� ��������� ������������� ��� Razor Pages
app.MapRazorPages();

app.Run();
