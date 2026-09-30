window.workCalendar = (function () {
    let _calendar = null;
    let _dotNetRef = null;
    let _days = new Map();
    let _labels = {};

    function dayKey(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return year + '-' + month + '-' + day;
    }

    function label(key, fallback) {
        return _labels[key] || fallback || '';
    }

    function formatLabel(key, value, fallback) {
        return label(key, fallback).replace('{0}', value ?? '');
    }

    function clearCustomContent(cell) {
        cell.querySelectorAll('.fcc-calendar-day-content, .fcc-calendar-issue-marker').forEach(x => x.remove());
    }

    function appendLine(container, text, className) {
        if (!text) return;

        const line = document.createElement('div');
        line.className = className || 'fcc-calendar-day-line';
        line.textContent = text;
        container.appendChild(line);
    }

    function renderCell(arg) {
        const key = dayKey(arg.date);
        const day = _days.get(key);

        clearCustomContent(arg.el);

        if (!day) return;

        const top = arg.el.querySelector('.fc-daygrid-day-top');
        const frame = arg.el.querySelector('.fc-daygrid-day-frame');

        if (!frame) return;

        const content = document.createElement('div');
        content.className = 'fcc-calendar-day-content';

        if (day.holiday) {
            appendLine(content, day.holidayName || label('holiday', 'Holiday'), 'fcc-calendar-day-line fcc-holiday-line');
        }

        if (day.shift) {
            appendLine(content, formatLabel('shift', day.shift, 'Shift: {0}'), 'fcc-calendar-day-line fcc-shift-line');
        }

        if (day.summary) {
            appendLine(content, day.summary, 'fcc-calendar-day-line fcc-summary-line');
        }

        if (day.registrationStatus) {
            appendLine(content, day.registrationStatus, 'fcc-calendar-day-line fcc-registration-line');
        }

        if (day.issue) {
            appendLine(content, day.issue, 'fcc-calendar-day-line fcc-issue-line');
        }

        if (content.childNodes.length > 0) {
            frame.appendChild(content);
        }
    }

    function setDays(days) {
        _days = new Map();
        (days || []).forEach(function (day) {
            _days.set(day.date, day);
        });
    }

    function buttonText() {
        return {
            today: label('today', 'Today'),
            month: label('month', 'Month'),
            list: label('list', 'List')
        };
    }

    function init(element, dotNetRef, days, initialDate, locale, labels) {
        // Blazor can invoke JS after the component has been rendered once but
        // before the referenced DOM node is connected (navigation/prerender/re-render).
        // FullCalendar 6 expects a live HTMLElement and otherwise throws:
        // "Cannot read properties of null (reading 'isConnected')".
        if (!element || !element.isConnected) {
            return false;
        }

        if (typeof FullCalendar === 'undefined' || !FullCalendar.Calendar) {
            return false;
        }

        _dotNetRef = dotNetRef;
        _labels = labels || {};
        setDays(days);

        if (_calendar) {
            _calendar.destroy();
            _calendar = null;
        }

        _calendar = new FullCalendar.Calendar(element, {
            initialView: 'dayGridMonth',
            initialDate: initialDate,
            locale: locale || 'vi',

            headerToolbar: {
                left: 'prev,next today',
                center: 'title',
                right: 'dayGridMonth,listMonth'
            },

            buttonText: buttonText(),
            height: 'auto',
            selectable: false,
            events: [],

            dateClick: function (info) {
                if (_dotNetRef) {
                    _dotNetRef.invokeMethodAsync(
                        'OnCalendarDateClick',
                        info.dateStr);
                }
            },

            datesSet: function (info) {
                if (_dotNetRef) {
                    _dotNetRef.invokeMethodAsync(
                        'OnCalendarRangeChanged',
                        info.startStr,
                        info.endStr);
                }
            },

            dayCellClassNames: function (arg) {
                const key = dayKey(arg.date);
                const day = _days.get(key);
                const classes = [];
                const weekDay = arg.date.getDay();

                if (weekDay === 6) {
                    classes.push('fcc-saturday');
                } else if (weekDay === 0) {
                    classes.push('fcc-sunday');
                }

                if (day?.holiday) {
                    classes.push('fcc-company-holiday');
                }

                if ((day?.registrations || []).some(
                    x => String(x.moduleCode).toUpperCase() === 'LEAVE')) {
                    classes.push('fcc-leave-day');
                }

                if (day?.canRegister) {
                    classes.push('fcc-registration-open');
                }

                if ((day?.issues || []).some(x => Number(x.severity || 0) >= 3)) {
                    classes.push('fcc-day-critical');
                } else if ((day?.issues || []).some(x => Number(x.severity || 0) === 2)) {
                    classes.push('fcc-day-warning');
                }

                return classes;
            },

            dayCellDidMount: renderCell
        });

        _calendar.render();
        return true;
    }

    function dispose() {
        if (_calendar) {
            _calendar.destroy();
            _calendar = null;
        }

        if (_dotNetRef) {
            _dotNetRef.dispose?.();
            _dotNetRef = null;
        }

        _days.clear();
        _labels = {};
    }

    return {
        init: init,
        dispose: dispose
    };
})();