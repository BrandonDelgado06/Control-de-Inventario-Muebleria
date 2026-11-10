import { alpha, createTheme, type PaletteMode } from '@mui/material/styles'

// Verde laca y ámbar de pino. En modo oscuro se aclaran para mantener el contraste sobre fondos oscuros.
const colores = {
  light: {
    primario: '#1F6F5C', textoPrimario: '#FFFFFF', secundario: '#A86A12',
    fondo: '#F4F6F3', papel: '#FFFFFF', menu: '#FAFBF9',
    texto: '#1C2624', textoSecundario: '#5A6763', divisor: '#D6DCD7',
  },
  dark: {
    primario: '#52B699', textoPrimario: '#0E1513', secundario: '#E2A64B',
    fondo: '#111715', papel: '#18201E', menu: '#141B19',
    texto: '#E5ECE9', textoSecundario: '#9AA8A3', divisor: '#2B3633',
  },
} as const

export function crearTema(modo: PaletteMode) {
  const c = colores[modo]
  return createTheme({
    palette: {
      mode: modo,
      primary: { main: c.primario, contrastText: c.textoPrimario },
      secondary: { main: c.secundario },
      background: { default: c.fondo, paper: c.papel },
      text: { primary: c.texto, secondary: c.textoSecundario },
      divider: c.divisor,
    },
    shape: { borderRadius: 8 },
    typography: {
      fontFamily: 'Roboto, "Segoe UI", system-ui, sans-serif',
      h1: { fontSize: '1.6rem', fontWeight: 700 },
      h2: { fontSize: '1.2rem', fontWeight: 600 },
      button: { textTransform: 'none', fontWeight: 600 },
    },
    components: {
      MuiPaper: { defaultProps: { variant: 'outlined' }, styleOverrides: { root: { backgroundImage: 'none' } } },
      MuiButton: { styleOverrides: { root: { whiteSpace: 'nowrap', flexShrink: 0 } } },
      MuiTableCell: { styleOverrides: { head: { fontWeight: 600, color: c.textoSecundario, whiteSpace: 'nowrap' } } },
      MuiTextField: { defaultProps: { size: 'small', fullWidth: true } },
      MuiSelect: { defaultProps: { size: 'small' } },
      MuiDrawer: { styleOverrides: { paper: { backgroundColor: c.menu } } },
      MuiAppBar: { styleOverrides: { root: { backgroundImage: 'none' } } },
    },
  })
}

/** Fondo suave del color principal: elemento seleccionado del menú, íconos de los indicadores. */
export const fondoPrimario = (opacidad = 0.12) => (t: { palette: { primary: { main: string } } }) => alpha(t.palette.primary.main, opacidad)
