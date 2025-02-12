// Monitoring.UI/Program.cs
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 1) ���������� RazorPages
builder.Services.AddRazorPages();

// 2) ���������� MemoryCache (���� �����������)
builder.Services.AddMemoryCache();

// 3) ������������ ���� ������� �� Infrastructure
//    ��������� ��������� -> ����������
builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<ILoginService, LoginService>();

// 4) ������������ ����������� � �� (���� appsettings.json, �� ��� ���)
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// ��� ������ ��������� ���� � appsettings.Development.json � �.�.

// ���� ������, ������ �������� ���������:
// builder.Services.Configure<...>(...) // ��� ������ ������� � builder.Configuration

var app = builder.Build();

// ���� �� � Dev-������, �������� ��������� ������
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