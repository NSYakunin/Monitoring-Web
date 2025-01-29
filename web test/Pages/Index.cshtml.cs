using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Numerics;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        // ������: ���� ������ ���������� �������� ������������� � View
        public string DepartmentName { get; set; } = "����� ����������";

        // ���� ����� ������������ ������� (GET/POST), �������������� ������:
        public void OnGet()
        {
            // ������, ������� ����������� ��� �������� ��������
            // ��������, ��������� ������, ���������� ViewModel � �.�.
        }

        // public IActionResult OnPost...()
        // ���� ����� ��������� ����, ����� ������� ��������������� ������ POST
    }
}
