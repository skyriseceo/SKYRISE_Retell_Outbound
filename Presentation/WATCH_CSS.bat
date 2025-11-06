@echo off
echo Watching Tailwind CSS for changes...
echo Press Ctrl+C to stop
node node_modules/tailwindcss/lib/cli.js -i ./wwwroot/css/input.css -o ./wwwroot/css/site.css --watch
