using System.Linq;
using Aethelgard.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Aethelgard.Tests;

/// <summary>
/// Журнал с вкладками: асинхронные вкладки (одно событие — в несколько
/// вкладок) и лимит 200 сообщений на вкладку.
/// </summary>
public class ChatTests
{
    [Fact]
    public void Event_Multiplexes_Into_Several_Tabs_And_All()
    {
        var chat = new GameChat();
        chat.Loot("С Крыса выпало: dagger");   // «Бой» + «Предметы» (+ «Всё»)

        Assert.Single(chat.For(GameChat.Tab.Combat));
        Assert.Single(chat.For(GameChat.Tab.Items));
        Assert.Empty(chat.For(GameChat.Tab.Social));
        Assert.Empty(chat.For(GameChat.Tab.Xp));
        Assert.Single(chat.All);
        Assert.Contains("Крыса", chat.For(GameChat.Tab.Combat)[0].Text);
        Assert.Contains("Крыса", chat.All[0].Text);
    }

    [Fact]
    public void Tab_Keeps_Last_200_Messages()
    {
        var chat = new GameChat();
        for (int i = 1; i <= 250; i++)
            chat.Combat($"hit {i}");

        var tab = chat.For(GameChat.Tab.Combat);
        Assert.Equal(200, tab.Count);
        Assert.Equal("hit 51", tab[0].Text);          // первые 50 вытеснены
        Assert.Equal("hit 250", tab[^1].Text);
        // «Всё» хранит те же последние 200
        Assert.Equal(200, chat.All.Count);
        Assert.Equal("hit 250", chat.All[^1].Text);
    }

    [Fact]
    public void Tabs_Are_Independent()
    {
        var chat = new GameChat();
        chat.Xp("Получено 5 опыта");
        chat.Item("Использовано: potion_s");
        chat.Social("[Разбойник] привет");

        Assert.Single(chat.For(GameChat.Tab.Xp));
        Assert.Single(chat.For(GameChat.Tab.Items));
        Assert.Single(chat.For(GameChat.Tab.Social));
        Assert.Empty(chat.For(GameChat.Tab.Combat));
        Assert.Equal(3, chat.All.Count);
    }
}
