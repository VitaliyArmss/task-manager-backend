# Task Manager Backend

ASP.NET Core Web API для приложения Task Manager. Backend обрабатывает запросы клиента, выполняет авторизацию с помощью JWT и хранит данные пользователей и задач в PostgreSQL.

**[Демо](https://vitaliyarmss.github.io/task-manager-frontend/)** · **[Frontend](https://github.com/VitaliyArmss/task-manager-frontend)** · **[Главный репозиторий](https://github.com/VitaliyArmss/task-manager-project)**

## Технологии

- C# и ASP.NET Core Web API
- JWT-аутентификация
- PostgreSQL

## Основные задачи backend

- обработка запросов frontend;
- авторизация пользователя и выдача токена;
- работа с данными пользователей и задач;
- взаимодействие с PostgreSQL.

## Локальный запуск

Требуются .NET SDK и доступный экземпляр PostgreSQL.

```bash
dotnet restore
dotnet run
```

Команды нужно выполнять в папке с `.csproj`. Если проект находится во вложенной папке, укажи путь к файлу проекта явно.

Перед запуском настрой строку подключения к PostgreSQL и параметры JWT через локальную конфигурацию или переменные окружения.

## Конфигурация

В production используются параметры подключения к базе данных и JWT, задаваемые через переменные окружения. Не коммить пароли, JWT secret и другие секреты в репозиторий. Production-конфигурация должна храниться отдельно от исходного кода.

## Production

Backend развёрнут на VPS. Nginx принимает HTTPS-запросы по префиксу `/task-manager/` и проксирует их в ASP.NET Core API на внутреннем порту `5001`.

Базовый адрес API:

```text
https://api.vitaliy-armss.ru/task-manager/api
```

Для frontend необходимо настроить CORS для соответствующего origin, например `https://vitaliyarmss.github.io`, а для локальной разработки — `http://localhost:5173`.
