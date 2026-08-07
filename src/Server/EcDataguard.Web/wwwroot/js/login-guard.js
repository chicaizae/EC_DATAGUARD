// Bloquea el envio nativo del formulario de login mientras el runtime
// Blazor todavia no se ha cargado (evita el error 400 por antiforgery).
window.addEventListener('submit', (e) => {
    const target = e.target;
    if (target && target.tagName === 'FORM' && !window.Blazor) {
        e.preventDefault();
        e.stopPropagation();
        const hint = document.getElementById('login-loading-hint');
        if (hint) {
            hint.style.display = 'block';
        }
    }
}, true);
