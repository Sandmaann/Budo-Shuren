/** @type {import('tailwindcss').Config} */
module.exports = {
    content: ["-/../**/*.{razor,html,cshtml}"],
    theme: {
        // Erweitert das Theme um eigene Farben und Schriftarten
        extend: {
            colors: {
                'primary': '#2A6749',
                'error': '#DC3545',
            },
            transitionProperty: {
                'height': 'height'
            },
            fontFamily: {
                'yuji': 'Yuji Syuku',
                'ptsans': 'PT Sans'
            },
            height: {
                '18': '4.5rem',
                '75': '18.75rem',
                '100': '25rem',
                '112': '28rem',
            },
            width: {
                '18': '4.5rem',
                '75': '18.75rem',
                '100': '25rem',
                '112': '28rem',

            },
            spacing: {
                '18': '4.5rem',
                '75': '18.75rem',
                '112': '28rem',
            }
        }
    },
    plugins: [],
}

