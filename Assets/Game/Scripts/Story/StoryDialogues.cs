using UnityEngine;

/// <summary>
/// Все диалоги нулевой версии. Текст дословно из NEWstory1-2-3day.md; собственные реплики
/// не добавляются. Скрипты строятся по запросу, условия вычисляются в момент вечера.
/// </summary>
public static class StoryDialogues
{
    // ================= КОТОВ =================

    public static DialogueScript KotovVisit(int day)
    {
        switch (day)
        {
            case 1: return KotovDay1();
            case 2: return KotovDay2();
            case 3: return KotovDay3();
            default: return null;
        }
    }

    private static DialogueScript KotovDay1()
    {
        DialogueScript s = DialogueBuilder.Create("kotov_day1", "[17:59. Тиканье часов громче, ветер стихает. Три тихих стука.]");
        s.Say("КОТОВ", "Вечер добрый. Вам письмо.");
        s.Choose(
            DialogueBuilder.Option("Взять открытку", null, null, "take_letter_1"),
            DialogueBuilder.Option("«Заходите, погрейтесь»", "«Нельзя мне. Дом выстудишь»", null, "kotov_decline"),
            DialogueBuilder.Option("Отвести взгляд", null, null, "gaze_break"));

        bool stoveLit = DayLoopManager.Instance != null && DayLoopManager.Instance.IsStoveLitToday();
        s.Say("КОТОВ", stoveLit ? "Хорошо у тебя печь дышит." : "Холодно у тебя. Перевал этого не любит.");
        s.Note("*Он знает, как меня зовут.*");
        return s;
    }

    private static DialogueScript KotovDay2()
    {
        DialogueScript s = DialogueBuilder.Create("kotov_day2", "[18:00. Три тихих стука.]");
        s.Say("КОТОВ", "Тяжёлый сегодня снег.");
        s.Choose(
            DialogueBuilder.Option("«Вы откуда идёте?»", "«Сверху. Как всегда»", null, null),
            DialogueBuilder.Option("«Кто вы?»", "«Почта»", null, null),
            DialogueBuilder.Option("Взять письмо", null, null, "take_letter_2"));

        bool morningDone = DayLoopManager.Instance != null && DayLoopManager.Instance.IsMorningDone(2);
        s.Say("КОТОВ", morningDone ? "Ты чистишь. Я вижу." : "Снег ждать не будет. А я подожду.");
        return s;
    }

    private static DialogueScript KotovDay3()
    {
        DialogueScript s = DialogueBuilder.Create("kotov_day3", "[18:00. Вой ветра у двери проваливается в тишину. Три чётких стука.]");
        s.Say("КОТОВ", "Громко сегодня.");
        s.Choose(
            DialogueBuilder.Option("«Вам не холодно?»", "«Уже нет»", null, null),
            DialogueBuilder.Option("Взять письмо", null, null, "take_letter_3"));
        s.Note("*На нём ни снежинки.*");
        s.Say("КОТОВ", "Дрова есть?");
        s.Choose(
            DialogueBuilder.Option("Кивнуть", "«Хорошо. Ночь длинная»", "day3_woodshed_full", null),
            DialogueBuilder.Option("«Мало»", "«Успеешь. Огонь подождёт, если попросить»", "day3_woodshed_low", null));
        return s;
    }

    public static string KotovGazeHeldLine(int day)
    {
        switch (day)
        {
            case 1: return "Завтра снег тяжелее будет.";
            case 2: return "Ты смотришь. Многие отворачиваются.";
            case 3: return "У сосны лежит чужое. Вернуть бы.";
            default: return null;
        }
    }

    // ================= ДЯДЯ КОТЛЕТА =================

    public static DialogueScript KotletaGreeting()
    {
        DialogueScript s = DialogueBuilder.Create("kotleta_greeting", "[Дядя Котлета — колонка №2]");
        s.Say("КОТЛЕТА", "«О. Живой смотритель. 80л бензина, малой. И кофе. Двойной, чёрный, чтоб горячий».");
        s.Say("КОТЛЕТА", "«Ну и мороз у вас. В кабине печка орёт, а толку...»");
        s.Choose(
            DialogueBuilder.Option("«Внутри теплее»", "«Чую. От двери печкой пахнет»", null, null),
            DialogueBuilder.Option("Кивнуть", "«Немногословный. Уважаю»", null, null));
        return s;
    }

    public static DialogueScript KotletaFuelReaction(FuelResult result)
    {
        DialogueScript s = DialogueBuilder.Create("kotleta_fuel");
        s.Say("КОТЛЕТА", result == FuelResult.Underfilled ? "«Маловато, малой. Давай заправляй заново»."
            : result == FuelResult.Accepted ? "«Не полный бак, но сойдёт»."
            : result == FuelResult.Perfect ? "«Вот теперь хорошо. Спасибо»."
            : "«Малой, у меня бак, а не озеро. Следи лучше».");
        return s;
    }

    public static DialogueScript KotletaItemReaction(bool kettleHot)
    {
        DialogueScript s = DialogueBuilder.Create("kotleta_coffee");
        s.Say("КОТЛЕТА", kettleHot ? "«Вот это я понимаю»." : "«Печку покорми, голодная она у тебя».");
        return s;
    }

    public static DialogueScript KotletaFarewell()
    {
        DialogueScript s = DialogueBuilder.Create("kotleta_farewell");
        s.Say("КОТЛЕТА", "«Пойду на горы гляну. Сколько лет мимо езжу, ни разу не постоял».");
        s.Note("[Через окно видно: он у сосны снимает перчатку и трогает кору. Возвращается и дует на голую лапу.]");
        s.Choose(DialogueBuilder.Option("Посмотреть на лапу (2 сек)", null, null, "notice_bare_paw"));
        s.Choose(DialogueBuilder.Option("Отвести взгляд", null, null, null));
        return s;
    }

    // ================= КУРЬЕР ВБ =================

    public static DialogueScript CourierGreeting()
    {
        DialogueScript s = DialogueBuilder.Create("courier_greeting", "[Курьер ВБ — колонка №1]");
        s.Say("КУРЬЕР", "«Слушай, шеф, где тут ПВЗ? Навигатор сюда тычет. \"Отделение связи «Перевал»\"».");
        s.Choose(
            DialogueBuilder.Option("«Тут заправка»", "«Да вижу! Какое отделение, тут сосна и колонка»", null, null),
            DialogueBuilder.Option("«Почты нет»", "«Нету? Вот тайга проклятая»", null, null));
        s.Say("КУРЬЕР", "«Плесни тридцатку и пачку печенья дай. До города дотянуть».");
        return s;
    }

    public static DialogueScript CourierFuelReaction(FuelResult result)
    {
        DialogueScript s = DialogueBuilder.Create("courier_fuel");
        s.Say("КУРЬЕР", result == FuelResult.Underfilled ? "«Маловато будет. Давай сначала, шеф»."
            : result == FuelResult.Accepted ? "«Не полный бак, но доеду. Погнал»."
            : result == FuelResult.Perfect ? "«Пять звёзд, шеф. Вот это заправка»."
            : "«Шеф, у меня бак, а не озеро. Лишнего налил».");
        return s;
    }

    public static DialogueScript CourierFarewell()
    {
        DialogueScript s = DialogueBuilder.Create("courier_farewell");
        s.Say("КУРЬЕР", "«О, щель для писем. Может, сюда скинуть? Шучу».");
        return s;
    }

    // ================= ДАНИЛА =================

    public static DialogueScript DanilaGreeting()
    {
        DialogueScript s = DialogueBuilder.Create("danila_greeting", "[Тонированный угловатый седан, стекло опускается на ладонь.]");
        s.Say("ДАНИЛА", "«Слышь, братан, заправь двадцатку. И сигарет дай».");
        return s;
    }

    public static DialogueScript DanilaLegend()
    {
        DialogueScript s = DialogueBuilder.Create("danila_legend", "[Пока идёт заправка, выходит, закуривает, смотрит на сосну.]");
        s.Say("ДАНИЛА", "«Дед мой рассказывал, тут раньше почтовая станция стояла. Почтальон ходил чудной, письма в любую пургу носил. Брешут старики или было такое?»");
        s.Choose(
            DialogueBuilder.Option("«Не слыхал»", "«Вот и я думаю, брешут»", null, "danila_legend_answer"),
            DialogueBuilder.Option("«Может, и было»", "«Дед говорил, его в шесть вечера тут ждали. Как по часам»", null, "danila_legend_answer"),
            DialogueBuilder.Option("«Тут каждый вечер кто-то стучит»", "(пауза) «...Не шути так. Я и так сюда еле поехал»", null, "danila_legend_answer"));
        s.Say("ДАНИЛА", "«Дед его по фамилии помнил. А я забыл. На \"К\" что-то. Бывай».");
        return s;
    }

    public static DialogueScript DanilaFuelReaction(FuelResult result)
    {
        DialogueScript s = DialogueBuilder.Create("danila_fuel");
        s.Say("ДАНИЛА", result == FuelResult.Underfilled ? "«Братан, маловато. Давай заново»."
            : result == FuelResult.Accepted ? "«Не полный бак, но пойдёт»."
            : result == FuelResult.Perfect ? "«Чётко. Вот так и надо»."
            : "«Братан, я двадцать просил, а ты мне озеро залил».");
        return s;
    }

    // ================= КРАСНЫЙ ПИКАП =================

    public static DialogueScript PickupArrival()
    {
        DialogueScript s = DialogueBuilder.Create("pickup_arrival", "[Из пурги проступают фары. Красный пикап встаёт у колонок, мотор глохнет.]");
        s.Note("[Серый полосатый кот, иней на усах, лёгкая куртка, его трясёт.]");
        s.Say("ВОДИТЕЛЬ", "«Д-добрый... Думал, не доеду. Фары ваши увидел и сам повернул».");
        s.Say("ВОДИТЕЛЬ", "«Мне бы канистру бензина про запас. И грелку. Пальцев не чувствую».");
        return s;
    }

    public static DialogueScript PickupAtStove()
    {
        DialogueScript s = DialogueBuilder.Create("pickup_stove");
        s.Choose(
            DialogueBuilder.Option("«Погрейтесь у печи»",
                StationHeatSystem.Active != null && StationHeatSystem.Active.CurrentHeat >= 50f
                    ? "«...Вот. Теперь живой»."
                    : "«Тут тоже не жарко, да». (Подкиньте полено в печь.)",
                null, "pickup_warm_stove"));
        return s;
    }

    public static DialogueScript PickupPayment()
    {
        DialogueScript s = DialogueBuilder.Create("pickup_payment", "Оплата:");
        s.Choose(
            DialogueBuilder.Option("Взять деньги", "«Вот. Спасибо»", null, "pickup_pay"),
            DialogueBuilder.Option("«Грелку оставьте»", "«Я верну. Летом заеду»", null, "pickup_pay"));
        return s;
    }

    public static DialogueScript PickupFarewell()
    {
        DialogueScript s = DialogueBuilder.Create("pickup_farewell");
        s.Say("ВОДИТЕЛЬ", "«А ты как будто ждал. У колонок».");
        s.Choose(
            DialogueBuilder.Option("«Радио сказало»", "(неуверенно смеётся) «Хорошее у вас радио»", null, "pickup_leave"),
            DialogueBuilder.Option("Промолчать", "[Кивает, уезжает]", null, "pickup_leave"));
        return s;
    }

    // ================= НОЧИ =================

    public static DialogueScript Night1Radio()
    {
        DialogueScript s = DialogueBuilder.Create("night1_radio", "[Ночь. Lo-fi джаз, лёгкое шипение. В паузе между треками — тихий вздох с эхом.]");
        s.Say("РАДИО", "«Слушай метель...»");
        s.Note("*Хвост Рыжикова дёргается, он подкручивает громкость. Показалось.*");
        s.Note("[Финальный кадр снаружи: тёплое окно станции, камера медленно уходит к сосне. Под сосной никого.]");
        s.Choose(DialogueBuilder.Option("Спать", null, null, "night_sleep"));
        return s;
    }

    public static DialogueScript Night2Radio()
    {
        DialogueScript s = DialogueBuilder.Create("night2_radio", "[23:00. Выключатель щёлкает сам, шкала загорается, стрелка ползёт к 87.3. Сквозь помехи — голос с тем же эхом, что у Котова.]");
        s.Say("РАДИО", "«...Красный пикап на перевале... у колонок... помоги ему...»");
        s.Note("[Помехи, щелчок, шкала гаснет.] Рыжиков садится в кресле прямо. *Я его выключал. Точно выключал.*");
        s.Choose(
            DialogueBuilder.Option("Подкрутить ручку настройки", "[Голос ручка не убирает. Шкала гаснет сама.]", null, null),
            DialogueBuilder.Option("Выключить радио", null, null, "night_sleep"));
        return s;
    }

    public static DialogueScript Night3Silhouette()
    {
        DialogueScript s = DialogueBuilder.Create("night3_silhouette", "[Дверь закрылась. Через окно у сосны виден тёмный высокий силуэт.]");
        s.Choose(
            DialogueBuilder.Option("Отвести взгляд и посмотреть снова", "[Его нет.]", null, "night3_look_again"),
            DialogueBuilder.Option("Выйти во двор", null, null, "night3_go_out"));
        return s;
    }

    public static DialogueScript Night3Footprints()
    {
        DialogueScript s = DialogueBuilder.Create("night3_footprints", "[Снаружи фонарь раскачивается, ветер выдул рыхлый снег, и над настом выступают большие старые следы: от стоянки грузовика к сосне. Между корнями из сугроба торчит кожаный манжет. Там, где стоял силуэт, снег гладкий.]");
        s.Note("*Тут кто-то стоял. А снег ровный.*");
        if (GameState.NoticedBarePaw)
        {
            s.Note("*Котлетина.*");
        }
        s.Choose(DialogueBuilder.Option("Вернуться", null, null, null));
        return s;
    }

    public static DialogueScript Night3End()
    {
        DialogueScript s = DialogueBuilder.Create("night3_end", "[Ночью ветер бьёт в окна, печь гудит. Рыжиков открывает блокнот. На завтрашнем, ещё пустом листе «Смены» уже есть строка карандашом, и её писал не он:]");
        s.Note(GameState.HasGlove ? "«Вернуть чужое»." : "«У сосны лежит чужое».");
        s.Choose(DialogueBuilder.Option("Закрыть блокнот", null, null, "night_sleep"));
        return s;
    }

    public static DialogueScript SliceEndCard()
    {
        DialogueScript s = DialogueBuilder.Create("slice_end", "[Затемнение.]");
        s.Note("«Перевал помнит. Продолжение следует».");
        s.Choose(DialogueBuilder.Option("Продолжить", null, null, "slice_card_close"));
        return s;
    }

    // ================= ОБЫЧНЫЕ МАШИНЫ =================

    public static DialogueScript RegularCarGreeting(string carName, float liters)
    {
        DialogueScript s = DialogueBuilder.Create("regular_greeting", $"[{carName} у колонки]");
        s.Say(carName.ToUpperInvariant(), $"«{liters:0} литров, пожалуйста».");
        return s;
    }

    public static DialogueScript RegularCarResult(FuelResult result)
    {
        DialogueScript s = DialogueBuilder.Create("regular_result");
        switch (result)
        {
            case FuelResult.Underfilled: s.Say("", "«Маловато. Давай начнём заправку заново»."); break;
            case FuelResult.Accepted: s.Say("", "«Не полный бак, но нормально»."); break;
            case FuelResult.Perfect: s.Say("", "«Хорошо заправили. Спасибо»."); break;
            default: s.Say("", "«У меня бак, а не озеро. Следите за объёмом»."); break;
        }
        return s;
    }

    public static DialogueScript TooSlowWarning()
    {
        DialogueScript s = DialogueBuilder.Create("too_slow");
        s.Say("", "«Шеф, у меня слот горит». (Без последствий.)");
        return s;
    }

    // ================= ЗАМЕТКИ РЫЖИКОВА =================

    public static string NightNote(int day)
    {
        switch (day)
        {
            case 1: return "«Печь растопил с третьей спички. Дальнобойщик пьёт кофе, как воду. В шесть приходил почтальон. Откуда тут почта?»";
            case 2: return "«Курьер искал ПВЗ, не нашёл. Радио включилось само. Я его выключал».";
            case 3: return "«Метель. Пикап был красный. Как сказали»." +
                           (GameState.HasGlove ? " «Под сосной никого. Следов тоже. Только старые, большие»." : "");
            default: return null;
        }
    }
}
