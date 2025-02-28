using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
//using Monitoring.Infrastructure.Services.Fake;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Monitoring.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ������������ �������
builder.Services.AddMonitoringServices(builder.Configuration);

var app = builder.Build();

// ���� �� � Dev-������, �������� ��������� ������
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ��� https
//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

app.Run();