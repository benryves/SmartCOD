# SmartCOD

The 65C02-based Economatics SmartBox is a computer control interface that allows the user to load and execute custom code via its serial port.
This code is normally stored in a COD file (e.g. AL.COD from the SmartMove software).
As it is not known ahead of time where in memory your code is going to be loaded this COD file contains a code stub that calls the SmartBox OS's relocation routine along with a bitmap of relocation data.

To create the single COD file containing your job's code, the relocation routines and the relocation bitmap the SmartCOD tool needs:
1. The "master" machine code for your job assembled at reference origin &0100.
2. The "difference" machine code for your job assembled at some other origin (e.g. &0200).
3. The offset to the relocation routine's entry point in the assembled program. This should be stored in the first two bytes of the program (little-endian).

The [SmartBox OS documentation](https://github.com/Phipli/SmartBox/issues/2#issuecomment-597111920) goes into detail about how such jobs should be written, but here is the sample code in a form that can be assembled using [beebasm](https://github.com/stardot/beebasm/):

```
no_of_calls = 1
:
VIA = &E030
ACIA = &E010
ADC = &E000
AUX_PORT = &E020
brk_vec = &200
nmi_vec = &202
irq_vec = &204
irq2_vec = &206
sendserial_vec = &208
readserial_vec = &20A
sendjob_vec = &20C
readjob_vec = &20E
decodejob_vec = &210
unknownjob_vec = &212
extjob_vec = &214
centisec_vec = &216
internal_vec = &218
callos_vec = &21A
printer_vec = &21C
zero_gp1 = 0
zero_gp2 = 2
zero_gp3 = 4
zero_gp4 = 6
zero_gp5 = 8
zero_gp6 = 10
zero_gp7 = 12
zero_gp8 = 14
zero_gp9 = 16
zero_gp10 = 18
user_reserved = &70
irq_A = &A0
fcount = &A1
RAM_size = &A3
jobout_buf = &400
jobin_buf = &480
OS_PRINTER = &FFB9
OS_CALLOS = &FFBC
OS_SENDBYTE = &FFBF
OS_READBYTE = &FFC2
OS_SENDJOB = &FFC5
OS_READJOB = &FFC8
OS_DECODEJOB = &FFCB
:
FOR create, 1, 2
	address=&100*create
	ORG address
	EQUW execcall-address
	:
	.job_DemoJob
	CMP #1
	BEQ DemoJob_go
	LDA #1
	LDY #0
	RTS
	\
	.DemoJob_go
	;
	;
	; Insert your job code here.
	;
	;
	RTS
	:
	.internal_handle
	CMP #1; Is it function call 1 (NameCode) ?
	BNE internal_handle_2; No, check for function 2
	\
	LDA job_id; Get our JOB ID number
	STA zero_gp3; Place it in zero_gp3 for CALLOS
	LDX #job_names MOD 256; Our Job name table (LSB)
	LDY #job_names DIV 256; Our Job name table (MSB)
	LDA #2; CALLOS function 2
	JSR OS_CALLOS; Call CALLOS
	BCC internal_yes; Found, go off and claim call
	LDA #1; Not found, restore A
	JMP (ovec); Call back to old vector
	\
	.internal_handle_2
	CMP #2; Is it function call 2 (CodeName) ?
	BNE internal_handle_3; No, unknown, pass on
	\
	LDA job_id; Get our JOB ID
	STA zero_gp3; Place it in zero_gp3 for CALLOS
	LDX #job_names MOD 256; Our Job name table (LSB)
	LDY #job_names DIV 256; Our Job name table (MSB)
	LDA #3; CALLOS function 3
	JSR OS_CALLOS; Call CALLOS
	BCC internal_yes; Found, go off and claim call
	LDA #2; Not found, restore A
	JMP (ovec); Call back to old vector
	\
	.internal_yes
	LDA #0; We want to claim call for some reason
	\
	.internal_handle_3
	JMP (ovec); Call back to old vector
	:
	.job_handle
	PHA; Make sure to preserve A
	CPY call; Check call wanted against base of ours
	BCC job_handle_no; Less than our base, so definately not ours
	TYA
	SBC #no_of_calls; Subtract number of calls we have
	CMP call; Now compare with base
	BCC job_handle2; Yes, one of ours
	\
	.job_handle_no
	PLA; Restore A
	JMP (ovec2); Call back to old vector
	\
	.job_handle2
	TYA
	SEC
	SBC call; Subtract our base from call
	ASL A; Times by 2 for offset into table
	TAY; Place in Y
	LDA job_run_table,Y; Get LSB of routine
	STA zero_gp1; Store it for indirect jump (LSB)
	LDA job_run_table+1,Y; Get MSB of routine
	STA zero_gp1+1; Store it for indirect jump (MSB)
	PLA; Restore A
	JMP (zero_gp1); Call our relevant routine
	:
	.job_names; List of our JobNames
	EQUS "DemoJob":EQUB 13; Test job name
	\
	EQUB &FF; &FF - marks end of table
	:
	.job_run_table; List of routine addresses to match Jobs
	EQUW job_DemoJob; Test job routine
	:
	.call   : EQUB 0; Store for our JobCode base
	.job_id : EQUB 0; Store for our JobId
	.ovec   : EQUW 0; Store for old internal_vec value
	.ovec2  : EQUW 0; Store for old unknownjob_vec value
	:
	EQUS STRING$(&100-(P% MOD 256),CHR$(0))
	.end_of_code; end_of_code should be on page boundry
	:
	.execute
	LDX #no_of_calls; Number of JobCodes wanted
	LDA #1; CALLOS, function 1, Claim JobCodes
	JSR OS_CALLOS; Call CALLOS
	STX call; Store base address of JobCodes allocated
	STY job_id; Store JOB ID given
	CPX #0; Check we were given some codes
	BEQ noinstall; No, don't bother installing ourself
	\
	LDA internal_vec; Get old internal_vec (LSB)
	STA ovec; Store it
	LDA internal_vec+1; Get old internal_vec (MSB)
	STA ovec+1; Store it
	LDA #internal_handle MOD 256; Our internal handler (LSB)
	STA internal_vec; Place it in the vector (LSB)
	LDA #internal_handle DIV 256; Our internal handler (MSB)
	STA internal_vec+1; Place it in the vector (MSB)
	\
	LDA unknownjob_vec; Get old unknownjob_vec (LSB)
	STA ovec2; Store it
	LDA unknownjob_vec+1; Get old unknownjob_vec (MSB)
	STA ovec2+1; Store it
	LDA #job_handle MOD 256; Our unknown job handler (LSB)
	STA unknownjob_vec; Place it in the vector (LSB)
	LDA #job_handle DIV 256; Our unknown job handler (MSB)
	STA unknownjob_vec+1; Place it in the vector (MSB)
	\
	LDX #end_of_code MOD 256; End of code (LSB)
	LDY #end_of_code DIV 256; End of code (MSB)
	STX jobin_buf; Place it in jobin_buf (LSB)
	STY jobin_buf+1; Place it in jobin_buf+1 (MSB)
	LDA #65; JobCode for WriteLomem
	JSR OS_DECODEJOB; Call JobCall handler
	\
	.noinstall
	RTS; End - return to OS
	:
	.execcall
	LDA #<(execute-address); Final execution address offset (LSB)
	STA zero_gp1
	LDA #>(execute-address); Final execution address offset (MSB)
	STA zero_gp1+1
	\
	LDA #<(BitMap-address); BitMap address offset (LSB)
	STA zero_gp2
	LDA #>(BitMap-address); BitMap address offset (MSB)
	STA zero_gp2+1
	\
	LDA #<(execcall-address); Length of data to relocate (LSB)
	STA zero_gp3
	LDA #>(execcall-address); Length of data to relocate (MSB)
	STA zero_gp3+1
	\
	TXA; Execcall REAL address
	SEC
	SBC #<(execcall-address); Subtract length
	TAX; Put it back in X
	TYA; Ditto for MSB
	SBC #>(execcall-address)
	TAY
	LDA #4; CALLOS function for Relocate
	JMP OS_CALLOS; Call CALLOS
	\
	.BitMap; BitMap start
	:
	SAVE "code"+STR$(create),address,*
	CLEAR address,*
NEXT
```

When assembled the above program will output two files, code1 and code2.
These can then be combined into a single DEMO.COD file which is then loaded to the SmartBox with the following SmartCOD command-lines:

```
SmartCOD -cod DEMO.COD build
SmartCOD -cod DEMO.COD -port COM1 load
SmartCOD -port COM1 list
```

The request to "list" will show all jobs (by number and name) currently available on the SmartBox so you can verify that DemoJob has been installed.

It's also possible to combine the three separate commands into a single command-line, without needing to repeat options:

```
SmartCOD -cod DEMO.COD build -port COM1 load list
```

Running the job that you have installed on the SmartBox is currently outside the scope of this tool as that will depend on what inputs your job accepts and what outputs it generates which cannot be determined ahead of time.
Running your installed job will generally follow this process:

1. Use the NameCode job on the SmartBox to get the call number of your job: send the byte 3, then the name of your job as an ASCII string terminated with CR (13), then read one byte back. That returned byte will have the call number.
2. Send the call number byte to the SmartBox, then any input data that your job requires (the job will retrieve the input data from the caller via OS_READJOB).
3. Read back as many bytes from the SmartBox as your job is designed to return (the job will send output data to the caller via OS_SENDJOB).

## Command-line usage

```
SmartCOD -option value command
```

Option names start with a `-dash` and all options must precede the corresponding command.

Options are retained after executing a command, so for example if you are building and loading a .COD file in one command line you only need to specify `-cod <filename>` before the build command and it will still be set when the load command runs.

If you need to unset an option you can do so manually with the `-unset <option>` option.

### BUILD

Build a .COD file from a "master" and "difference" compiled file.

| Option                   | Description                                                     |
|--------------------------|-----------------------------------------------------------------|
| `-cod <filename>`        | Filename of the .COD file to generate.                          |
| `-master <filename>`     | Filename of the master code file (optional, defaults to code1). |
| `-difference <filename>` | Name of the difference code file (optional, defaults to code2). |

```
SmartCOD -cod DEMOJOB.COD build
```

### LOAD

Downloads a .COD file to the SmartBox and executes it.

| Option             | Description                                    |
|--------------------|------------------------------------------------|
| `-cod <filename>`  | Filename of the .COD file to load and execute. |
| `-port <portname>` | Name of the SmartBox's serial port.            |

```
SmartCOD -cod DEMOJOB.COD -port COM1 load
```

### LIST

Lists jobs on the connected SmartBox.

| Option             | Description                              |
|--------------------|------------------------------------------|
| `-port <portname>` | Name of the SmartBox's serial port.      |

```
SmartCOD -port COM1 list
```

### RESET

Resets the SmartBox.

| Option                     | Description                                                        |
|----------------------------|--------------------------------------------------------------------|
| `-reset hard/soft/<value>` | Hard reset clears battery-backed RAM (optional, defaults to soft). |
| `-port <portname>`         | Name of the SmartBox's serial port.                                |

```
SmartCOD -port COM1 -reset hard reset
```