using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRazorPages(options =>
{
    // ��������� ��������� Antiforgery-�����
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});

var app = builder.Build();


app.MapRazorPages();

app.Run();
