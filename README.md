# Мониторинг
![.NET Version](https://img.shields.io/badge/.NET-9.0-blueviolet)
![Razor Pages](https://img.shields.io/badge/Razor%20Pages-Enabled-success)
![Entity Framework](https://img.shields.io/badge/Entity%20Framework-Latest%20Version-green)
![MS SQL](https://img.shields.io/badge/Database-MS%20SQL-blue)

Добро пожаловать в репозиторий проекта **Мониторинг**! 

## Содержание
1. [Описание проекта](#описание-проекта)
2. [Основные особенности](#основные-особенности)
3. [Используемые технологии](#используемые-технологии)
4. [Структура проекта](#структура-проекта)
5. [Установка и запуск](#установка-и-запуск)
6. [Использование](#использование)
7. [Планы по развитию](#планы-по-развитию)
8. [Скриншоты (при наличии)](#скриншоты)
9. [Вклад (Contributing)](#вклад-contributing)
10. [Лицензия](#лицензия)

---

## Описание проекта
Проект **Мониторинг** — это веб-приложение, написанное на языке **C#** под **.NET 9**, использующее **Razor Pages**, **Entity Framework** (последней версии), **HTML**, **JavaScript** и базу данных **MS SQL**.  

Основное предназначение проекта:  
- Авторизация пользователя (логин и пароль)  
- Отображение главного экрана с большой таблицей работ за выбранный период
- Отображение активных уведомлений в отдельно небольшой таблице
- Возможность "руками" перетаскивать элементы основной таблицы, ручная сортировка + выбор необходимых строк для отчета
- Создание заявок на корректировку по дате (или закрытие) выбранной работы, добавление заметок к заявке и выбор человека для рассмотрения.
- Вывод входящих заявок, и их рассмотрение (отклонение или принятие)
- Поля фильтрации и сортировки (по исполнителям, а также строка поиска)  
- Генерация **PDF**-отчёта с помощью библиотеки **QuestPDF**  

Проект пока что размещён в локальной сети на внутреннем домене и находится в активной разработке.

---

## Основные особенности
- **Аутентификация**: Защищённый вход по логину и паролю.  
- **Удобное отображение**: На главном экране доступна сводная таблица с работами за требуемый период (время начала и окончания).  
- **Фильтрация и поиск**:  
  - Фильтр по исполнителям  
  - Строка ввода для поиска по загруженным данным  
- **Быстрая генерация PDF**: Используется библиотека [QuestPDF](https://github.com/QuestPDF/QuestPDF) для создания отчётов.  
- **Гибкая архитектура**: Проект легко расширять и дополнять новым функционалом.

---

## Используемые технологии
| Технология               | Описание                                                  |
|--------------------------|-----------------------------------------------------------|
| **C# / .NET 9**             | Основной язык и платформа, на которой построен проект.   |
| **Razor Pages**             | Фреймворк для построения веб-интерфейсов на ASP.NET.     |
|**Entity Framework, ADO.NET**| ORM для взаимодействия с базой данных.                   |
| **MS SQL**                  | Реляционная СУБД для хранения данных.                    |
| **HTML, CSS, JavaScript**   | Разметка и скрипты на клиентской стороне.                |
| **QuestPDF**, **OpenXml**   | Генерация PDF-отчётов.                                   |

---

## Структура проекта
```plaintext
Monitoring.sln
 ├─ Monitoring.Domain
 │   └─ Entities
 │       ├─ WorkItem.cs
 |       ├─ WorkRequest.cs
 │       └─ Notification.cs
 |       
 ├─ Monitoring.Application
 │   ├─ Interfaces
 │   │   ├─ IWorkItemService.cs
 │   │   ├─ IUserSettingsService.cs
 │   │   ├─ ILoginService.cs
 |   |   ├─ IWorkRequestService.cs
 │   │   └─ INotificationService.cs
 │   ├─ DTO
 │   │   ├─ CreateRequestDto.cs
 |   |   ├─ DivisionDto.cs
 |   |   ├─ PrivacySettingsDto.cs
 |   |   └─ StatusChangeDto.cs
 │   ├─ Services
 │   │   └─ ReportGenerator.cs
 |   |   ├─ ReportGeneratorExcel.cs
 |   |   └─ ReportGeneratorWord.cs
 │   └─ ...
 ├─ Monitoring.Infrastructure
 │   ├─ Services
 |   |   ├─ NotificationService.cs
 |   |   ├─ WorkRequestService.cs
 │   │   ├─ UserSettingsService.cs
 │   │   ├─ WorkItemService.cs
 │   │   └─ LoginService.cs
 │   └─ ...
 └─ Monitoring.UI
     ├─ Pages
     |   ├─ Shared
     |   |   ├─ _Layout.cshtml
     |   |   └─ _WorkItemsTablePartial.cshtml
     │   ├─ Index.cshtml
     │   ├─ Index.cshtml.cs
     │   ├─ Login.cshtml
     │   ├─ Login.cshtml.cs
     │   ├─ Settings.cshtml
     │   ├─ Settings.cshtml.cs
     │   └─ Shared Partials (партиалы)
     ├─ wwwroot
     ├─ appsettings.json
     └─ Program.cs

