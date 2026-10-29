# Nowcast Pure

Твой трек из любого плеера в статусе Discord - безопасно, плавно и без чужих серверов.

[![GitHub release](https://img.shields.io/github/v/release/Frozen-starTony/NowcastPure?color=5865F2&style=flat-square)](https://github.com/Frozen-starTony/NowcastPure/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-3d3c38?style=flat-square)](https://github.com/Frozen-starTony/NowcastPure)
[![Status](https://img.shields.io/badge/Status-In%20Active%20Development-orange?style=flat-square)](https://github.com/Frozen-starTony/NowcastPure/issues)

---

> [!IMPORTANT]
> **Проект находится в активной разработке.**  
> Базовый функционал работает стабильно: плавный таймер, поиск обложек в высоком разрешении (Яндекс Музыка, Deezer, Apple), режим AFK, очистка названий и кнопки.
> 
> **Есть идеи или предложения по улучшению?**  
> Пишите свои идеи в [**Issues**](https://github.com/Frozen-starTony/NowcastPure/issues/new?template=feature_request.yml) или [**Discussions**](https://github.com/Frozen-starTony/NowcastPure/discussions).

---

## Возможности

- **Поддержка любых плееров Windows:**
  - Яндекс Музыка (Desktop и браузер)
  - Spotify
  - YouTube и YouTube Music
  - VK Музыка
  - SoundCloud
  - Apple Music / iTunes
  - AIMP, Foobar2000, VLC, Windows Media Player
- **Официальные HD-обложки (512x512):**  
  Прямой поиск через официальные защищенные CDN (Яндекс Музыка, Deezer, Apple). Никаких сторонних серверов и VPS.
- **Плавный таймер:**  
  Счетчик секунд тикает плавно, синхронно и без откатов на ноль.
- **Двойные кнопки:**  
  1. *Слушать на YouTube* - универсальная ссылка для друзей.  
  2. *Яндекс Музыка / Spotify / VK* - прямой поиск в сервисе воспроизведения.
- **Очистка названий (De-cluttering):**  
  Автоматически убирает из названий визуальный мусор вроде `[Official Video]`, `(Official Audio)`, `(Slowed + Reverb)`.
- **Режим AFK на паузе:**  
  Когда трек на паузе или плеер выключен, статус переключается в режим AFK со счетчиком времени отдыха.
- **Фильтрация источников:**  
  Возможность исключить голосовые сообщения из Telegram и видео из браузеров.
- **Вес 35 КБ и 0% нагрузки:**  
  Написано на C# (.NET Framework 4.8), не требует сторонних библиотек и рантаймов.

---

## Безопасность и Приватность

| Параметр | Другие утилиты из сети | Nowcast Pure |
| :--- | :--- | :--- |
| **Слив на чужой сервер** | Загружают файлы на чужие VPS/IP | **0% (полная изоляция, нет чужих серверов)** |
| **Получение обложек** | Через сторонние хостинги | **Прямой HTTPS к официальным CDN (Yandex/Apple)** |
| **Размер исполняемого файла** | ~170-500 КБ | **Всего 35 КБ** |
| **Сборка** | Закрытые или неизвестные бинарники | **100% открытый код, сборка системным csc.exe** |

---

## Быстрый старт

### Вариант 1: Скачать готовый файл
Скачайте актуальный релиз **`NowcastPure.exe`** из раздела [Releases](https://github.com/Frozen-starTony/NowcastPure/releases) и запустите.

### Вариант 2: Собрать самостоятельно (без установки SDK)
В любую Windows 10/11 встроен компилятор .NET Framework 4.8 (`csc.exe`):

```powershell
git clone https://github.com/Frozen-starTony/NowcastPure.git
cd NowcastPure
powershell -ExecutionPolicy Bypass -File build.ps1
```

---

## Настройки (config.json)

Все параметры можно переключать через контекстное меню в системном трее:

```json
{
  "AfkMode": true,
  "IgnoreTelegram": true,
  "IgnoreBrowser": false,
  "CleanTitles": true
}
```

- `AfkMode` - статус AFK при паузе трека.
- `IgnoreTelegram` - скрывает голосовые сообщения и аудио из Telegram.
- `IgnoreBrowser` - игнорирует видео из браузеров (Chrome, Edge, Firefox).
- `CleanTitles` - вычищает скобки и лишний мусор из названий.

---

## Вклад и Предложения

Проект развивается. Мы рады вашим отзывам, PR и идеям.  
Используйте [Issues](https://github.com/Frozen-starTony/NowcastPure/issues) для сообщений об ошибках и предложений новых функций.

---

## Лицензия

Распространяется под лицензией [MIT](LICENSE).
