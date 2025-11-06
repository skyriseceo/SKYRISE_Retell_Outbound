/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        './Views/**/*.cshtml',   // هيقرأ كل ملفات الـ Views
        './Areas/**/*.cshtml'  // هيقرأ كل ملفات الـ Areas (بتاعة الـ Identity)
    ],
    theme: {
        extend: {},
    },
    plugins: [],
}
