import { createTheme } from '@mui/material/styles'

// Verde laca y ámbar de pino: los mismos tonos de la página del plan del proyecto.
export const tema = createTheme({
  palette: {
    primary: { main: '#1F6F5C', contrastText: '#FFFFFF' },
    secondary: { main: '#A86A12' },
    background: { default: '#F4F6F3', paper: '#FFFFFF' },
    text: { primary: '#1C2624', secondary: '#5A6763' },
    divider: '#D6DCD7',
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily: 'Roboto, "Segoe UI", system-ui, sans-serif',
    h1: { fontSize: '1.6rem', fontWeight: 700 },
    h2: { fontSize: '1.2rem', fontWeight: 600 },
    button: { textTransform: 'none', fontWeight: 600 },
  },
  components: {
    MuiPaper: { defaultProps: { variant: 'outlined' } },
    MuiButton: { styleOverrides: { root: { whiteSpace: 'nowrap', flexShrink: 0 } } },
    MuiTableCell: { styleOverrides: { head: { fontWeight: 600, color: '#5A6763', whiteSpace: 'nowrap' } } },
    MuiTextField: { defaultProps: { size: 'small', fullWidth: true } },
    MuiSelect: { defaultProps: { size: 'small' } },
  },
})
