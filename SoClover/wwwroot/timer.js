// Reusable floating phase countdown timer
// Injects a floating element at the top-right of the page and updates every second

(function(){
  const TIMER_ID = 'phaseCountdownTimer';
  const WARNING_THRESHOLD_SECONDS = 30;
  let interval = null;
  let deadlineUtc = null; // Date

  function ensureElement(){
    let el = document.getElementById(TIMER_ID);
    if (!el){
      el = document.createElement('div');
      el.id = TIMER_ID;
      el.className = 'phase-timer hidden';
      el.innerHTML = '<span class="phase-timer-label">⏳</span><span class="phase-timer-time">--:--</span>';
      document.body.appendChild(el);
    }
    return el;
  }

  function pad2(n){ return n < 10 ? '0' + n : '' + n; }

  function formatMMSS(totalSeconds){
    const clamped = Math.max(0, Math.floor(totalSeconds));
    const m = Math.floor(clamped / 60);
    const s = clamped % 60;
    return pad2(m) + ':' + pad2(s);
  }

  function tick(){
    const el = ensureElement();
    const timeSpan = el.querySelector('.phase-timer-time');
    if (!deadlineUtc){
      el.classList.add('hidden');
      timeSpan.textContent = '--:--';
      return;
    }
    const now = new Date();
    const remainingMs = deadlineUtc.getTime() - now.getTime();
    const remainingSec = Math.ceil(remainingMs / 1000);

    if (remainingSec <= 0){
      // Reached or passed deadline
      timeSpan.textContent = '00:00';
      el.classList.remove('hidden');
      el.classList.add('warning');
      return; // keep visible at 00:00 until next poll updates phase
    }

    timeSpan.textContent = formatMMSS(remainingSec);
    el.classList.remove('hidden');

    if (remainingSec <= WARNING_THRESHOLD_SECONDS){
      el.classList.add('warning');
    } else {
      el.classList.remove('warning');
    }
  }

  function start(){
    if (interval) return;
    interval = setInterval(tick, 1000);
    tick();
  }

  function stop(){
    if (interval){
      clearInterval(interval);
      interval = null;
    }
  }

  // Public API
  window.initPhaseTimer = function(){
    ensureElement();
    start();
  };

  window.setPhaseDeadline = function(isoUtcString){
    if (!isoUtcString){
      deadlineUtc = null;
      const el = ensureElement();
      el.classList.add('hidden');
      return;
    }
    // Some backends return with timezone; Date can parse ISO 8601
    const parsed = new Date(isoUtcString);
    if (isNaN(parsed.getTime())){
      // Fallback: hide timer if invalid
      deadlineUtc = null;
      const el = ensureElement();
      el.classList.add('hidden');
      return;
    }
    deadlineUtc = parsed;
    start();
  };

  window.disposePhaseTimer = function(){
    stop();
    const el = document.getElementById(TIMER_ID);
    if (el && el.parentElement){
      el.parentElement.removeChild(el);
    }
  };
})();
