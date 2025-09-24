// Esempio di utilizzo del timezone helper

import { 
  convertUTCToItalianTimezone, 
  isItalianDST, 
  getItalianTimezoneOffset,
  formatItalianDate,
  formatGameDateTime,
  debugTimezoneConversion,
  ITALIAN_TIMEZONE_CONSTANTS
} from '../utils/timezoneHelpers';

/**
 * Esempi di utilizzo delle utility del timezone
 */
export function timezoneExamples() {
  console.log('🌍 Esempi di utilizzo Timezone Helper:');
  console.log('=' .repeat(50));

  // Esempio 1: Conversione base
  const utcDate = "2024-03-15T20:30:00Z";
  const italianDate = convertUTCToItalianTimezone(utcDate);
  console.log(`📅 UTC: ${utcDate}`);
  console.log(`🇮🇹 Italiano: ${italianDate}`);

  // Esempio 2: Controllo ora legale
  const winterDate = new Date("2024-01-15T15:00:00Z");
  const summerDate = new Date("2024-07-15T15:00:00Z");
  
  console.log(`❄️ Inverno (Gennaio): UTC+${getItalianTimezoneOffset(winterDate)}`);
  console.log(`☀️ Estate (Luglio): UTC+${getItalianTimezoneOffset(summerDate)}`);

  // Esempio 3: Formattazione per partite
  const matchDate = convertUTCToItalianTimezone("2024-03-17T20:45:00Z");
  if (matchDate) {
    console.log(`⚽ Partita: ${formatGameDateTime(matchDate)}`);
  }

  // Esempio 4: Debug completo
  debugTimezoneConversion("2024-03-31T01:30:00Z"); // Cambio ora legale

  // Esempio 5: Utilizzo delle costanti
  console.log(`📊 Costanti disponibili:`);
  console.log(`   - Offset invernale: UTC+${ITALIAN_TIMEZONE_CONSTANTS.WINTER_OFFSET}`);
  console.log(`   - Offset estivo: UTC+${ITALIAN_TIMEZONE_CONSTANTS.SUMMER_OFFSET}`);
  console.log(`   - Cambio ora alle: ${ITALIAN_TIMEZONE_CONSTANTS.CHANGE_HOUR}:00`);
}

/**
 * Converte una lista di date per il calendario Serie A
 */
export function convertSerieADates(utcDates: string[]): string[] {
  return utcDates
    .map(date => convertUTCToItalianTimezone(date))
    .filter((date): date is string => date !== null);
}

/**
 * Ottiene informazioni sul timezone per una data specifica
 */
export function getTimezoneInfo(date: Date | string) {
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  
  return {
    isDST: isItalianDST(dateObj),
    offset: getItalianTimezoneOffset(dateObj),
    formatted: formatItalianDate(dateObj),
    gameFormat: formatGameDateTime(dateObj)
  };
}