# Как внести вклад в «Этельгард»

Мы рады любому вкладу: сообщениям об ошибках, предложениям по улучшению, пул-реквестам с кодом и документацией.

## Требования

- **.NET SDK** — версия указана в `global.json` (рекомендуется .NET 8.0 или новее).
- **MonoGame SDK** — шаблоны и инструменты MonoGame (устанавливаются через `dotnet new install MonoGame.Templates.CSharp`).
- **IDE** — Visual Studio 2022, JetBrains Rider или VS Code с расширением C#.
- **Git** — для работы с репозиторием.

## Настройка окружения

1. Клонируйте репозиторий:
   bash
   git clone https://github.com/Valerka3421/rpg.git
   cd rpg
2. Восстановите зависимости:
   bash
   dotnet restore          
3. Соберите проект:
   bash
   dotnet build
4. Запустите игру (убедитесь, что путь к проекту верный):
   bash
   dotnet run --project src/Ethelgard/Ethelgard.csproj
