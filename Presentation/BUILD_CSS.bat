@echo off
echo Building Tailwind CSS...
node node_modules/tailwindcss/lib/cli.js -i ./wwwroot/css/input.css -o ./wwwroot/css/site.css --minify
echo Done!
pause
