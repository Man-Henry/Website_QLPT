// Admin Sidebar Toggle
const sidebar = document.getElementById('adminSidebar');
const mainArea = document.getElementById('adminMain');
const toggleBtn = document.getElementById('sidebarToggle');

toggleBtn?.addEventListener('click', () => {
    document.body.classList.toggle('sidebar-collapsed');
});

// Auto toast dismiss
document.querySelectorAll('.toast-qlpt').forEach(toast => {
    setTimeout(() => {
        toast.style.animation = 'slideInRight 0.3s ease reverse';
        setTimeout(() => toast.remove(), 300);
    }, 4000);
});
