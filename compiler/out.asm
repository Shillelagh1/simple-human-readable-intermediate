; #EXTREQ std 
extern malloc

; #BADCI 
; #PROC helloworld 
; #LOCALARR byte myString 
; #NOBADCI 
; new myString Hello World! 
; print myString 
; #FREEARR myString 
; #ENDPROC 
; #PROC START 
; #ENTRYPARAMS 
; call helloworld 
; #EXIT 
; #ENDPROC 
