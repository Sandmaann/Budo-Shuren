/** @type {import('tailwindcss').Config} */
module.exports = {
    content: ['-/../**/*.{razor,html,cshtml}' ],

    variants: {
        extend: {
            scale: ['active'],
        },
        display: ['responsive', 'group-hover', 'group-focus'],
    },
    theme: {
        // Erweitert das Theme um eigene Farben und Schriftarten
        extend: {
            screens: {
                'xs': '390px',
                '3xl': '1800px',
                '4xl': '2600px',
                '5xl': '3000px',
                sidebarHeight: {
                    raw: '(min-height: 953px)'
                }

            },
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
                '112': '28rem',
                '120': '30rem',
                '128': '32rem',
            },
            width: {
                '18': '4.5rem',
                '22' : '5.5rem',
                '75': '18.75rem',
                '100': '25rem',
                '112': '28rem',
                '120': '30rem',
                '128': '32rem',
            },
            spacing: {
                '18': '4.5rem',
                '22' : '5.5rem',
                '75': '18.75rem',
                '112': '28rem',
                '128': '32rem',
            },
            margin: {
                calc: 'min(-1em, (100vw - 768pc) / -2)',
            },
            scale: {
                '85': '0.85',
            }
        }
    },
    plugins: []
}

