namespace NFSU1ProfileMaster;

public static class Strings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Lang = new()
    {
        ["IT"] = new()
        {
            ["title"] = "NFSU1 Profiles Master",
            ["file_btn"] = "FILE...", ["file_none"] = "(nessun file selezionato)",
            ["tab_unlock"] = "SBLOCCHI", ["tab_car"] = "AUTO", ["tab_custom"] = "STATISTICHE",
            ["lbl_name"] = "NOME:", ["lbl_date"] = "DATA:", ["lbl_bank"] = "SOLDI:", ["lbl_change"] = "CAMBIA IN:",
            ["chk_unique_cars"] = "Sblocca tutte le auto unique",
            ["rad_none"] = "Lascia gli eventi come sono",
            ["rad_advance"] = "Avanza / torna indietro all'evento", ["lbl_of"] = "di 112",
            ["rad_manual"] = "Eventi manuali: evento", ["chk_passed"] = "completato",
            ["rad_complete"] = "Completa tutti gli eventi",
            ["btn_guide"] = "GUIDA ALLO SBLOCCO",
            ["hint_game"] = "Chiudi il gioco (o carica un altro profilo) prima di scrivere il salvataggio, altrimenti il gioco lo sovrascrive di nuovo.",
            ["car_engine"] = "Motore", ["car_transmission"] = "Trasmissione", ["car_tires"] = "Gomme", ["car_ecu"] = "ECU",
            ["car_turbo"] = "Turbo", ["car_brakes"] = "Freni", ["car_weight"] = "Alleggerimento", ["car_suspension"] = "Sospensioni", ["car_nos"] = "NOS",
            ["car_unique"] = "Unique",
            ["row_starts"] = "Partenze", ["row_wins"] = "Vittorie", ["row_style"] = "Style points", ["row_totaled"] = "Volte distrutta",
            ["tot_starts"] = "Partenze totali", ["tot_wins"] = "Vittorie totali", ["tot_losses"] = "Sconfitte totali", ["tot_style"] = "Style points totali",
            ["btn_patch"] = "SCRIVI SUL FILE", ["btn_about"] = "INFO", ["btn_exit"] = "ESCI",
            ["dlg_open"] = "Seleziona il salvataggio (.ugd)",
            ["msg_select"] = "Seleziona prima un file di salvataggio (.ugd).",
            ["msg_invalid"] = "Non e' un salvataggio NFS7 valido (dimensione o hash errati).",
            ["msg_wrong_value"] = "Valore errato.\nInserisci un intero tra 0 e 2147483647.",
            ["msg_ok"] = "Profilo aggiornato.",
            ["msg_fail"] = "Qualcosa e' andato storto.\n\nPer ripristinare: elimina {0} e rinomina {0}.bak in {0}.",
            ["msg_io"] = "Impossibile leggere o scrivere il file: controlla che non sia in uso o di sola lettura.",
            ["t_warn"] = "ATTENZIONE", ["t_error"] = "ERRORE", ["t_done"] = "COMPLETATO", ["t_about"] = "INFO", ["t_guide"] = "GUIDA",
            ["yes"] = "SI", ["no"] = "NO", ["ok"] = "OK", ["cancel"] = "ANNULLA",
            ["status_loaded"] = "Salvataggio caricato.",
            ["about_text"] = "NFSU1 Profiles Master {0}\nby GabriLex\n\nEditor di salvataggi per Need for Speed: Underground (NFS7).\n\nIl formato dei file .ugd e' stato studiato partendo da \"NFS7: Underground Savegame Editor v1.1\" di mift0 (2003-2004).",
            ["guide_text"] = "Eventi: auto, piste e upgrade disponibili dipendono dal numero di eventi completati, e vengono sbloccati per numero (non per tipo di gara).\n\nUn modo comodo: avanza fino all'evento che sblocca quello che ti serve, compra o monta le parti nel gioco, poi torna indietro al numero di eventi che avevi.\n\nEventi manuali: scegli un evento e segnalo come completato o no; le modifiche si applicano quando premi SCRIVI SUL FILE.\n\nAuto unique: il pulsante le segna tutte come sbloccate.\n\nFai sempre un backup: il programma crea comunque il file .bak accanto al salvataggio.",
        },
        ["EN"] = new()
        {
            ["title"] = "NFSU1 Profiles Master",
            ["file_btn"] = "FILE...", ["file_none"] = "(no file selected)",
            ["tab_unlock"] = "UNLOCK", ["tab_car"] = "CAR", ["tab_custom"] = "STATS",
            ["lbl_name"] = "NAME:", ["lbl_date"] = "DATE:", ["lbl_bank"] = "BANK:", ["lbl_change"] = "CHANGE TO:",
            ["chk_unique_cars"] = "Unlock all unique cars",
            ["rad_none"] = "Leave events as they are",
            ["rad_advance"] = "Advance / draw back to event", ["lbl_of"] = "of 112",
            ["rad_manual"] = "Manual events: event", ["chk_passed"] = "passed",
            ["rad_complete"] = "Complete all events",
            ["btn_guide"] = "UNLOCKING GUIDE",
            ["hint_game"] = "Close the game (or load another profile) before writing the save, otherwise the game overwrites it again.",
            ["car_engine"] = "Engine", ["car_transmission"] = "Transmission", ["car_tires"] = "Tires", ["car_ecu"] = "ECU",
            ["car_turbo"] = "Turbo", ["car_brakes"] = "Brake kits", ["car_weight"] = "Weight reduction", ["car_suspension"] = "Suspension", ["car_nos"] = "NOS",
            ["car_unique"] = "Unique",
            ["row_starts"] = "Starts", ["row_wins"] = "Wins", ["row_style"] = "Style points", ["row_totaled"] = "Times totaled",
            ["tot_starts"] = "Total starts", ["tot_wins"] = "Total wins", ["tot_losses"] = "Total losses", ["tot_style"] = "Total style points",
            ["btn_patch"] = "PATCH FILE", ["btn_about"] = "ABOUT", ["btn_exit"] = "EXIT",
            ["dlg_open"] = "Select the savegame (.ugd)",
            ["msg_select"] = "Select a savegame file (.ugd) first.",
            ["msg_invalid"] = "This is not a valid NFS7 savegame (wrong size or hash).",
            ["msg_wrong_value"] = "Wrong value.\nEnter an integer between 0 and 2147483647.",
            ["msg_ok"] = "Profile updated.",
            ["msg_fail"] = "Something went wrong.\n\nTo restore: delete {0} and rename {0}.bak to {0}.",
            ["msg_io"] = "Cannot read or write the file: check that it is not in use or read-only.",
            ["t_warn"] = "WARNING", ["t_error"] = "ERROR", ["t_done"] = "DONE", ["t_about"] = "ABOUT", ["t_guide"] = "GUIDE",
            ["yes"] = "YES", ["no"] = "NO", ["ok"] = "OK", ["cancel"] = "CANCEL",
            ["status_loaded"] = "Savegame loaded.",
            ["about_text"] = "NFSU1 Profiles Master {0}\nby GabriLex\n\nSavegame editor for Need for Speed: Underground (NFS7).\n\nThe .ugd file format was studied starting from \"NFS7: Underground Savegame Editor v1.1\" by mift0 (2003-2004).",
            ["guide_text"] = "Events: the cars, tracks and upgrades available depend on how many events you have completed, and they unlock by number (not by race type).\n\nA handy trick: advance to the event that unlocks what you need, buy or fit the parts in the game, then go back to the number of events you had.\n\nManual events: pick an event and mark it passed or not; changes are applied when you press PATCH FILE.\n\nUnique cars: the checkbox marks all of them as unlocked.\n\nAlways keep a backup: the program also creates a .bak file next to the save.",
        },
    };

    public static string Current { get; set; } = "IT";
    public static string Get(string key) => Lang[Current][key];
}
