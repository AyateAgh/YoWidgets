# Проверка уточнений к YoWidgets

19 октября 2026 года. Редакция 3: добавлена проверка PS 3.0, COM обоев, Sparse Package и обратимости установки. Выполнен анализ источников и документации; рендерер, приложение и сборка пока не созданы. Ни один пункт ниже не является результатом runtime-тестирования готового приложения.

## Liquid Glass — изменение исходного предложения

Первый вариант с Acrylic и статическим двойным контуром недостаточен для уточнённого требования пользователя. В обновлённой спецификации он заменён обязательным шейдерным эффектом и ранней проверкой рабочего образца.

В исходниках MacShell обнаружена более конкретная схема, чем общий тезис «прозрачное окно Linux»: `WeatherWidget.qml` загружает обои, совмещает их с экранными координатами виджета, делает предварительное размытие и передаёт текстуру в `liquidglass.frag`. Фрагментный шейдер считает SDF, нормаль выпуклой кромки, преломление, RGB-дисперсию, линзовое смещение и блик. Таким образом, для соответствующих настольных виджетов референса постоянный захват чужих окон не нужен.

Предложение: тот же принцип вычисляемого преломления текстуры обоев, GPU-эффект только на фоне виджета, читаемый контент поверх него. При перетаскивании меняется область выборки, при изменении размера — геометрия линзы, при смене обоев — текстура. Это настоящий вычисляемый оптический эффект. Для неподвижного фона не нужна искусственная анимация каждого кадра.

Ограничение: такой источник не отражает автоматически сторонние видеообои и произвольные окна под плавающей панелью. Доступ к живому системному backdrop/другой источник надо проверять отдельно; незаметно включать захват всего экрана ради этой возможности нельзя. Предварительный выбор WPF не считается доказательством совместимости шейдера, WorkerW и DWM. До окончательного выбора рендерера нужен образец и замер.

Источники: [WeatherWidget.qml](https://github.com/0-ss/MacShell/blob/main/MacShell/WeatherWidget.qml), [liquidglass.frag](https://github.com/0-ss/MacShell/blob/main/MacShell/liquidglass.frag), [Apple: Materials](https://developer.apple.com/design/human-interface-guidelines/materials), [WPF ShaderEffect](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.shadereffect).

## Проверенные замечания

| Пункт | Результат проверки | Принятое решение |
|---|---|---|
| Snooze снижает активность | Управляемые приложением таймеры, запросы и анимации можно остановить. Нулевую стоимость существующего процесса и DWM гарантировать нельзя. | Обычный Snooze замораживает обновления, глубокий скрывает поверхности и освобождает ресурсы. Измерить CPU процесса и стоимость DWM отдельно. |
| Focus Assist через History | `ToastNotificationManager.History` — история уведомлений приложения, а не датчик системного DND. Notification listener также не является таким датчиком. | Не вычислять DND по наличию уведомлений; неизвестное состояние показывать неизвестным. |
| Управление Focus | `FocusSessionManager` находится в `Windows.UI.Shell`, имеет проверку поддержки; запуск/остановка отмечены как Limited Access Feature. Focus-сессия и DND не тождественны. | Собственный Pomodoro и переход к настройкам Windows; системное состояние только при реально доступном API. Не обещать обычный системный toggle без LAF. |
| Чужие уведомления | Listener поддерживает доступ к системным toast-уведомлениям, чтение и удаление после manifest capability и разрешения пользователя. | Необязательный модуль с package identity; отказ и отзыв разрешения обрабатываются. Тексты не сохраняются постоянно. |
| Блокирование системных всплытий | Описанный listener API сообщает о добавленных/удалённых уведомлениях; нет документированного события отмены системного показа до доставки. | Snooze подавляет только собственную визуализацию. Для штатного DND — настройки Windows. Не удалять чужие уведомления для имитации блокировки. |
| Громкость | Core Audio предоставляет endpoint volume/mute. NAudio — возможная обёртка, а не обязательное условие. | Только явные команды, проверка результата, смены устройства и отсутствия устройства. |
| Яркость | WMI предоставляет методы управления, но наличие провайдера зависит от оборудования/драйвера. На этом компьютере запрос вернул 1 экземпляр WmiMonitorBrightness. Запись яркости не проверялась. | Показывать ползунок только для подтверждённого устройства. Внешние мониторы не объявлять поддержанными без отдельной проверки; не добавлять DDC/CI автоматически. |
| Wi-Fi | Полноценный переключатель радио требует отдельной проверки доступа и аппаратной поддержки. Он также меняет состояние сети, что выходит за чисто визуальное действие. | В первой поставке честная кнопка открытия штатной Wi-Fi-панели; никаких скрытых изменений радио/сети. |
| ИИ | Раньше ИИ не был указан в исходном запросе; теперь он предложен явно. Фоновая локальная LLM противоречит цели лёгкой утилиты. | Необязательный отключённый модуль с явным провайдером, ручной отправкой и отменой; без автоматического доступа к уведомлениям и экрану. |
| Раскрытие плеера | Изменение размера/компоновки действительно есть в референсе. | Зафиксированы компактное и раскрытое состояния, длительность перехода, reduced motion и проверки границ/DPI. |
| AllowsTransparency | WPF поддерживает аппаратно ускоренные layered-окна; тезис «AllowsTransparency всегда означает программный рендеринг» неверен. Это не доказывает работу конкретной комбинации layered/DWM/backdrop. | Проверять фактический рендеринг и артефакты; не строить эффект на неподтверждённой комбинации флагов. |
| WPF-UI / AccentPolicy | UI-библиотека и светлая каёмка не реализуют преломление. Microsoft не рекомендует SetWindowCompositionAttribute, предлагая DwmSetWindowAttribute. | Использовать документированные API, где возможно. AccentPolicy не является основой стабильного Liquid Glass. |
| Win + D | Обычный non-topmost не определяет поведение настольного виджета. WorkerW/Progman — зависимость от деталей Explorer, а не гарантированный desktop-widget API. | Отдельный DesktopHost, ранняя проверка Win + D и восстановления. Не изменять и не перезапускать Explorer ради подключения. |
| SetParent + DPI | Microsoft предупреждает о неожиданном поведении при разных DPI-awareness; cross-process SetParent может сбрасывать DPI-awareness дочернего процесса. | PerMonitorV2, проверка контекстов до SetParent, отказ от подключения при несовместимости. Один манифест проблему reparenting полностью не решает. |
| Масштабирование | PerMonitorV2 рекомендуется задавать до создания HWND. Нужны также корректные координаты и пересчёт ресурсов. | DIP для компоновки, физические пиксели для текстур; WM_DPICHANGED; разные DPI, отрицательные координаты и смена монитора в тестах. |

## Граница «визуальная надстройка»

Требование отключения и полного удаления одной конечной кнопкой вынесено в [cleanup-contract.md](cleanup-contract.md). Оно включает Sparse identity, внешние файлы, data roots, ключи/кеши, автозапуск, dev-регистрации и проверку остатков. Это пока проектный контракт, а не проверенный очиститель.

Приложение не заменяет оболочку и экран блокировки, не устанавливает драйверы, не внедряет DLL в другие процессы и не меняет системные файлы. Запуск не требует повышения прав. Привязка собственных окон к десктопному слою — обратимая операция с собственными окнами; она всё равно требует проверки совместимости Explorer.

Рабочие ползунки громкости/яркости по явному действию меняют соответствующее состояние системы. Кнопки плеера управляют воспроизведением. Это необходимо для запрошенных функций и ясно обозначается в интерфейсе. При старте, скрытии и Snooze эти команды самостоятельно не выполняются. Чужая история уведомлений автоматически не очищается. Отключение системной защиты, shell patching, реестровые обходы DND и принудительный перезапуск Explorer исключены из проекта.

Фразы «вообще без багов», «никогда ничего не сломает», «абсолютный ноль нагрузки» и «пиксель в пиксель как Apple на любой системе» не являются проверяемыми гарантиями. Критерий выпуска — реальные функциональные, визуальные и нагрузочные испытания, отчёт с результатами и явно перечисленные неподдержанные сценарии.

## Что проверено на компьютере

- Каталог проекта содержит исходные видео и документы; готового приложения нет.
- Windows: версия 25H2, build 26200.9457. Реестровое ProductName ещё сообщает Windows 10 Pro; для определения поколения Windows это поле само по себе не используется.
- Ранее обнаружены RTX 3050 Laptop, .NET Desktop Runtime 8.0.22 и отсутствие SDK. Около 16 ГБ установленной памяти по Win32_ComputerSystem.
- WMI вернул 1 провайдер яркости. Уровни яркости, громкость, DND, Wi-Fi и конфигурация Explorer в ходе проверки не менялись.
- Преломление, расход GPU, доставка уведомлений, Win + D и разные мониторы ещё не проверены в работающей сборке. Их нельзя объявлять работающими по одному анализу API.
- Cmdlet Add-AppxPackage и Remove-AppxPackage доступны. В локальных parameter sets AddSet и RegisterSet есть ExternalLocation; RegisterSet также имеет Register и Path. Наличие синтаксиса не доказывает успешную регистрацию конкретного манифеста. Пакеты и сертификаты в этой проверке не регистрировались, не удалялись; Developer Mode не изменялся.

## Дополнительные три технических уточнения

**WPF/PS 3.0.** Ограничения выбранного API подтверждены. Но 512 инструкций не является универсальным максимумом стандарта: документация описывает ёмкость от 512 до device-specific MaxPixelShader30InstructionSlots, максимум 32768; dynamic flow control и loop/rep предусмотрены. WPF накладывает свои ограничения на samplers, формат и аппаратную поддержку. Поэтому нельзя ни обещать, что полный GLSL-проход переносится без изменений, ни объявлять его невозможным по одной цифре. Решение — короткий compile/render test с кешируемым preblur; при провале переход в Direct3D/DirectComposition. Простое наложение WPF-контролов на HwndHost/Direct3D подчиняется airspace; этот риск добавлен в ранний образец, чтобы не получить некликабельный или перекрытый интерфейс.

**IDesktopWallpaper.** Принято как основной COM-источник. GetWallpaper вызывается с monitorId из GetMonitorDevicePathAt; GetMonitorRECT даёт геометрию, GetPosition — общую политику размещения, GetBackgroundColor — цвет. GetWallpaper(NULL) может вернуть S_FALSE/пустую строку при разных обоях или slideshow. Пустая строка также допустима при сплошном фоне. Методы не принимают virtual desktop id, поэтому корректное обновление обоев при переключении столов — runtime-проверка, а не гарантированная возможность COM-интерфейса. SPI_GETDESKWALLPAPER не используется как основной источник нескольких экранов.

**Sparse Package.** Подтверждён как способ добавить identity, оставив EXE вне MSIX. Для разработки возможна регистрация layout-манифеста через Register + ExternalLocation; её prerequisites и реальная identity проверяются в пробном цикле. Для поставки документирован подписанный identity-only MSIX + ExternalLocation. Изменения только в коде EXE не требуют перепаковки полного приложения; изменение identity/манифеста может потребовать обновления регистрации. Assets и metadata identity должны совпадать с manifest приложения. Capability и отдельное согласие listener по-прежнему обязательны. Unregister выполняется по точной identity текущего пользователя; он не удаляет все внешние бинарники и созданные программой файлы — за это отвечает собственный cleanup.

Источники: [PS 3.0](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx9-graphics-reference-asm-ps-3-0), [WPF ShaderEffect](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.shadereffect), [WPF airspace](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/technology-regions-overview), [GetWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-idesktopwallpaper-getwallpaper), [GetPosition](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-idesktopwallpaper-getposition), [Sparse identity и unregister](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps), [Add-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage).

## Ссылки для воспроизведения проверки

- [Notification listener: capability, permission, events, remove](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener)
- [ToastNotificationManager.History](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.toastnotificationmanager.history)
- [FocusSessionManager и ограничения LAF](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.focussessionmanager)
- [WPF: hardware-accelerated layered windows](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/technology-regions-overview)
- [SetWindowCompositionAttribute — рекомендация использовать другой API](https://learn.microsoft.com/en-us/windows/win32/dwm/setwindowcompositionattribute)
- [SetParent: стили окон и DPI-awareness](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)
- [Манифест DPI-awareness](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process)
- [Core Audio: endpoint volume](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-volume-controls)
- [WMI: WmiSetBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmisetbrightness-method-in-class-wmimonitorbrightnessmethods)
- [Открытие штатных настроек Windows](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)
