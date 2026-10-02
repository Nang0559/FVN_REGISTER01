window.workCalendar = (function () {
    let _calendar = null;
    let _dotNetRef = null;
    let _days = new Map();
    let _labels = {};

    function dayKey(date) { const y=date.getFullYear(); const m=String(date.getMonth()+1).padStart(2,'0'); const d=String(date.getDate()).padStart(2,'0'); return y+'-'+m+'-'+d; }
    function label(key,fallback){return _labels[key]||fallback||'';}
    function formatLabel(key,value,fallback){return label(key,fallback).replace('{0}',value??'');}
    function clearCustomContent(cell){cell.querySelectorAll('.fcc-calendar-day-content,.fcc-calendar-issue-marker').forEach(x=>x.remove());}
    function appendLine(container,text,className){if(!text)return;const line=document.createElement('div');line.className=className||'fcc-calendar-day-line';line.textContent=text;if((className||'').includes('attendance-symbol')||(className||'').includes('ot-symbol')){line.style.fontWeight='800';line.style.fontSize='12px';line.style.lineHeight='1.15';}container.appendChild(line);}

    function renderCell(arg){
        const key=dayKey(arg.date), day=_days.get(key); clearCustomContent(arg.el); if(!day)return;
        const top=arg.el.querySelector('.fc-daygrid-day-top'), frame=arg.el.querySelector('.fc-daygrid-day-frame'); if(!frame)return;
        const issues=day.issues||[], critical=issues.some(x=>Number(x.severity||0)>=3), warning=issues.some(x=>Number(x.severity||0)===2);
        if(issues.length>0&&top){const marker=document.createElement('button');marker.type='button';marker.className='fcc-calendar-issue-marker '+(critical?'critical':warning?'warning':'info');marker.textContent='?';marker.title=label('issue','Issue requires attention');marker.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();if(_dotNetRef)_dotNetRef.invokeMethodAsync('OnCalendarIssueClick',key,issues[0].code);});top.appendChild(marker);}
        const body=document.createElement('div');body.className='fcc-calendar-day-content';
        if(day.holiday)appendLine(body,label('holiday','Holiday')+': '+day.holiday,'fcc-calendar-day-line holiday');
        if(day.shift)appendLine(body,formatLabel('shift',day.shift,'Shift {0}'),'fcc-calendar-day-line shift');
        if(day.attendance){
            const i=day.attendance.checkIn||'--:--',o=day.attendance.checkOut||'--:--';
            if(day.attendance.symbol)appendLine(body,String(day.attendance.symbol),'fcc-calendar-day-line attendance-symbol');
            if(day.attendance.otSymbol)appendLine(body,String(day.attendance.otSymbol),'fcc-calendar-day-line ot-symbol');
            appendLine(body,i+' → '+o,'fcc-calendar-day-line attendance');
            if(day.attendance.actualHours!=null&&day.attendance.requiredHours!=null)appendLine(body,Number(day.attendance.actualHours).toFixed(2).replace(/\.00$/,'')+'h / '+Number(day.attendance.requiredHours).toFixed(2).replace(/\.00$/,'')+'h','fcc-calendar-day-line hours');
            else if(day.attendance.display)appendLine(body,String(day.attendance.display),'fcc-calendar-day-line hours');
        }
        (day.registrations||[]).forEach(r=>{const prefix=r.isHalfDay?'½ ':'';const time=(r.start&&r.end)?' · '+r.start+'-'+r.end:'';appendLine(body,prefix+r.moduleCode+' · '+r.title+time,'fcc-calendar-day-line registration '+String(r.moduleCode).toLowerCase());const status=String(r.approvalStatus||'').toLowerCase();let statusText=r.approvalStatus||'';if(r.isApproved||status==='approved')statusText=label('approved',statusText);else if(status==='pending')statusText=label('pending',statusText);else if(status==='inprogress')statusText=label('inProgress',statusText);else if(status==='rejected')statusText=label('rejected',statusText);else if(status==='cancelled')statusText=label('cancelled',statusText);else if(status==='needsrevision')statusText=label('needsRevision',statusText);const parts=[statusText];if(r.approvalLevel!=null&&!r.isApproved)parts.push(formatLabel('level',r.approvalLevel,'Level {0}')+(r.approvalLevelName?' · '+r.approvalLevelName:''));if(r.currentApproverName&&!r.isApproved)parts.push(r.currentApproverName);if(parts.some(Boolean))appendLine(body,parts.filter(Boolean).join(' · '),'fcc-calendar-day-line registration-status '+String(r.moduleCode).toLowerCase());});
        if(day.canRegister)appendLine(body,label('register','+ Register'),'fcc-calendar-day-line registration-open'); if(body.childNodes.length>0)frame.appendChild(body);
    }
    function renderAllCells(){if(!_calendar)return;_calendar.el.querySelectorAll('.fc-daygrid-day').forEach(cell=>{const date=cell.getAttribute('data-date');if(date)renderCell({date:new Date(date+'T00:00:00'),el:cell});});}
    function setDays(days){_days=new Map();(days||[]).forEach(day=>_days.set(day.date,day));}
    function buttonText(){return{today:label('today','Today'),month:label('month','Month'),list:label('list','List')};}

    function resolveHost(hostOrId){
        if(typeof hostOrId==='string') return document.getElementById(hostOrId);
        if(hostOrId && hostOrId instanceof Element) return hostOrId;
        return null;
    }

    function scheduleInit(hostOrId,dotNetRef,days,initialDate,locale,labels,retry){
        retry=retry||0;
        const host=resolveHost(hostOrId);
        if(!host || !host.isConnected || !document.documentElement.contains(host) || typeof FullCalendar==='undefined' || !FullCalendar.Calendar){
            if(retry<40) setTimeout(()=>scheduleInit(hostOrId,dotNetRef,days,initialDate,locale,labels,retry+1),50);
            return;
        }
        requestAnimationFrame(()=>{
            const currentHost=resolveHost(hostOrId);
            if(!currentHost || !currentHost.isConnected || !document.documentElement.contains(currentHost)){
                if(retry<40) setTimeout(()=>scheduleInit(hostOrId,dotNetRef,days,initialDate,locale,labels,retry+1),50);
                return;
            }
            try {
                _dotNetRef=dotNetRef;_labels=labels||{};setDays(days);
                if(_calendar){_calendar.destroy();_calendar=null;}
                _calendar=new FullCalendar.Calendar(currentHost,{initialView:'dayGridMonth',initialDate:initialDate,locale:locale||'vi',headerToolbar:{left:'prev,next today',center:'title',right:'dayGridMonth,listMonth'},buttonText:buttonText(),height:'auto',selectable:false,events:[],dateClick:info=>{if(_dotNetRef)_dotNetRef.invokeMethodAsync('OnCalendarDateClick',info.dateStr);},datesSet:info=>{if(_dotNetRef){const viewStart=info.view.currentStart,viewEnd=info.view.currentEnd;_dotNetRef.invokeMethodAsync('OnCalendarRangeChanged',dayKey(viewStart),dayKey(viewEnd));}},dayCellClassNames:arg=>{const day=_days.get(dayKey(arg.date)),c=[],w=arg.date.getDay();if(w===6)c.push('fcc-saturday');else if(w===0)c.push('fcc-sunday');if(day?.holiday)c.push('fcc-company-holiday');if((day?.registrations||[]).some(x=>String(x.moduleCode).toUpperCase()==='LEAVE'))c.push('fcc-leave-day');if(day?.canRegister)c.push('fcc-registration-open');if((day?.issues||[]).some(x=>Number(x.severity||0)>=3))c.push('fcc-day-critical');else if((day?.issues||[]).some(x=>Number(x.severity||0)===2))c.push('fcc-day-warning');return c;},dayCellDidMount:renderCell});
                _calendar.render();
            } catch(error) {
                if(_calendar){try{_calendar.destroy();}catch{} _calendar=null;}
                if(retry<40) setTimeout(()=>scheduleInit(hostOrId,dotNetRef,days,initialDate,locale,labels,retry+1),100);
                else console.error('workCalendar.init failed after retries',error);
            }
        });
    }

    function init(elementOrId,dotNetRef,days,initialDate,locale,labels){scheduleInit(elementOrId,dotNetRef,days,initialDate,locale,labels,0);}
    function setLocale(locale,labels){_labels=labels||_labels;if(!_calendar)return;_calendar.setOption('locale',locale||'vi');_calendar.setOption('buttonText',buttonText());requestAnimationFrame(renderAllCells);}
    function updateDays(days){if(!_calendar)return;setDays(days);requestAnimationFrame(renderAllCells);}
    function destroy(){if(_calendar){_calendar.destroy();_calendar=null;}_dotNetRef=null;_days=new Map();_labels={};}
    return{init:init,setLocale:setLocale,updateDays:updateDays,destroy:destroy};
})();
