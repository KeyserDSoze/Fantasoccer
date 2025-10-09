// Timezone utilities per la gestione delle date italiane
// Converte date UTC+0 che contengono già l'orario italiano corretto
// in date con il timezone italiano appropriato (UTC+1 o UTC+2)

/**
 * Calcola l'anno corrente del fantacalcio
 * L'anno fantacalcio inizia il 10 agosto e finisce il 9 agosto dell'anno successivo
 * @returns Anno fantacalcio corrente (0 = 2011/2012, 1 = 2012/2013, ecc.)
 */
export function getActualFantacalcioYear(): number {
  const now = new Date();
  let actualYear = now.getFullYear() - 2011;
  
  // Se siamo prima del 10 agosto, siamo ancora nell'anno precedente
  const month = now.getMonth(); // 0-indexed (0 = gennaio, 7 = agosto)
  const day = now.getDate();
  
  if (month < 7 || (month === 7 && day < 10)) {
    actualYear--;
  }
  
  return actualYear;
}

/**
 * Converte una data UTC+0 che contiene già l'orario italiano corretto
 * in una data con il timezone italiano appropriato (UTC+1 o UTC+2)
 * @param utcDateString - Data in formato ISO string UTC+0 che contiene già l'orario italiano
 * @returns Data in formato ISO string con timezone italiano corretto
 */
export function convertUTCToItalianTimezone(utcDateString: string | null): string | null {
  if (!utcDateString) return null;
  
  try {
    // Parsa la data UTC+0 che contiene già l'orario italiano
    const utcDate = new Date(utcDateString);
    
    // Determina se siamo in ora solare (UTC+1) o legale (UTC+2)
    const year = utcDate.getFullYear();
    
    // L'ora legale in Italia va dall'ultima domenica di marzo all'ultima domenica di ottobre
    const dstStart = getLastSundayOfMonth(year, 2); // Marzo (0-indexed)
    const dstEnd = getLastSundayOfMonth(year, 9);   // Ottobre (0-indexed)
    
    // Controlliamo se la data è nel periodo dell'ora legale
    const isDST = utcDate >= dstStart && utcDate < dstEnd;
    
    // Calcoliamo l'offset in base al periodo
    const offsetHours = isDST ? 2 : 1; // UTC+2 in estate, UTC+1 in inverno
    const offsetMinutes = offsetHours * 60;
    
    // Creiamo una nuova data aggiungendo l'offset
    const italianDate = new Date(utcDate.getTime());
    
    // Formattiamo la data con il timezone italiano
    const offsetString = isDST ? '+02:00' : '+01:00';
    
    // Costruiamo manualmente la stringa ISO con il timezone corretto
    const year_str = italianDate.getUTCFullYear();
    const month = String(italianDate.getUTCMonth() + 1).padStart(2, '0');
    const day = String(italianDate.getUTCDate()).padStart(2, '0');
    const hours = String(italianDate.getUTCHours()).padStart(2, '0');
    const minutes = String(italianDate.getUTCMinutes()).padStart(2, '0');
    const seconds = String(italianDate.getUTCSeconds()).padStart(2, '0');
    
    return `${year_str}-${month}-${day}T${hours}:${minutes}:${seconds}${offsetString}`;
    
  } catch (error) {
    console.error('Errore nella conversione della data:', error);
    return utcDateString; // Ritorna la data originale in caso di errore
  }
}

export function convertUTCToItalianTimezoneAsNotNullableDate(utcDateString: string | null): Date {
  const currentDate = convertUTCToItalianTimezone(utcDateString);
  return currentDate ? new Date(currentDate) : new Date();
}

/**
 * Verifica se una data è nel periodo dell'ora legale italiana
 * @param date - Data da verificare
 * @returns true se è nel periodo dell'ora legale (UTC+2), false se ora solare (UTC+1)
 */
export function isItalianDST(date: Date): boolean {
  const year = date.getFullYear();
  const dstStart = getLastSundayOfMonth(year, 2); // Marzo
  const dstEnd = getLastSundayOfMonth(year, 9);   // Ottobre
  
  return date >= dstStart && date < dstEnd;
}

/**
 * Ottiene l'offset del timezone italiano per una data specifica
 * @param date - Data per cui calcolare l'offset
 * @returns Offset in ore (+1 per ora solare, +2 per ora legale)
 */
export function getItalianTimezoneOffset(date: Date): number {
  return isItalianDST(date) ? 2 : 1;
}

/**
 * Formatta una data con il timezone italiano appropriato
 * @param date - Data da formattare
 * @returns Stringa formattata con timezone italiano
 */
export function formatItalianDate(date: Date | string, showEverything: boolean = false): string {
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  
  if (showEverything) 
    return dateObj.toLocaleString('it-IT', {
      timeZone: 'Europe/Rome',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  else
    return dateObj.toLocaleDateString('it-IT', {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit'
    })
}

/**
 * Formatta una data per la visualizzazione nelle partite
 * @param date - Data da formattare
 * @returns Stringa formattata per UI (es. "Dom 15 Mar, 20:30")
 */
export function formatGameDateTime(date: Date | string | null): string {
  if (!date) return 'Data non disponibile';
  
  try {
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    
    return dateObj.toLocaleDateString('it-IT', {
      timeZone: 'Europe/Rome',
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit'
    });
  } catch {
    return 'Data non valida';
  }
}

/**
 * Ottiene l'ultima domenica di un mese specifico
 * @param year - Anno
 * @param month - Mese (0-indexed: 0=gennaio, 11=dicembre)
 * @returns Data dell'ultima domenica del mese
 */
function getLastSundayOfMonth(year: number, month: number): Date {
  // Ultimo giorno del mese
  const lastDay = new Date(year, month + 1, 0);
  
  // Trova l'ultima domenica
  const dayOfWeek = lastDay.getDay(); // 0 = domenica
  const daysToSubtract = dayOfWeek === 0 ? 0 : dayOfWeek;
  
  const lastSunday = new Date(year, month + 1, 0 - daysToSubtract);
  
  // Per l'ora legale, il cambio avviene alle 2:00 del mattino
  lastSunday.setHours(2, 0, 0, 0);
  
  return lastSunday;
}

/**
 * Debug utility - mostra informazioni dettagliate sulla conversione di una data
 * @param utcDateString - Data UTC+0 da analizzare
 */
export function debugTimezoneConversion(utcDateString: string): void {
  console.log('🧪 Debug conversione timezone:', utcDateString);
  console.log('=' .repeat(40));
  
  const original = new Date(utcDateString);
  const converted = convertUTCToItalianTimezone(utcDateString);
  const convertedDate = converted ? new Date(converted) : null;
  
  console.log(`📅 Originale (UTC+0): ${original.toISOString()}`);
  console.log(`🇮🇹 Convertito: ${converted}`);
  console.log(`🕐 Visualizzazione Italia: ${convertedDate ? formatItalianDate(convertedDate) : 'N/A'}`);
  console.log(`🌅 Ora legale: ${isItalianDST(original) ? 'Sì (UTC+2)' : 'No (UTC+1)'}`);
  console.log(`⏰ Offset: UTC+${getItalianTimezoneOffset(original)}`);
}

// Costanti utili
export const ITALIAN_TIMEZONE_CONSTANTS = {
  WINTER_OFFSET: 1,    // UTC+1 (ora solare)
  SUMMER_OFFSET: 2,    // UTC+2 (ora legale)
  DST_START_MONTH: 2,  // Marzo (0-indexed)
  DST_END_MONTH: 9,    // Ottobre (0-indexed)
  CHANGE_HOUR: 2       // Cambio alle 2:00 del mattino
} as const;