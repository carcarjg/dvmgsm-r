# Digital Voice Modem Global System for Mobile Communications – Railway

The Digital Voice Modem Global System for Mobile Communications – Railway (GSM-R), provides a WPF desktop application that mimics or otherwise operates like a typical GSM-R, allowing
DVM users to listen to one talkgroup on a DVM FNE from a single application using a Siemens GSM-R look and feel.


![image](https://github.com/user-attachments/assets/062c367a-cbfa-4fa7-a06f-4adf3abddf2c)



## Building

This project utilizes a standard Visual Studio solution for its build system.

The GSM-R software requires the library dependancies below. Generally, the software attempts to be as portable as possible and as library-free as possible. A basic Visual Studio install, with .NET is usually all that is needed to compile.

### Dependencies

- dvmvocoder (libvocoder); https://github.com/DVMProject/dvmvocoder

### Build Instructions

1. Clone the repository. `git clone --recurse-submodules https://github.com/carcarjg/dvmgsmr.git`
2. Switch into the "dvmconsole" folder.
3. Open the "dvmconsole.sln" with Visual Studio.
4. Select "x86" as the CPU type.
5. Compile.

Please note that while, x64 CPU types are supported, it will require compiling the dvmvocoder library separately for that CPU architecture.

## dvmconsole Configuration

1. Create/Edit `codeplug.yml` (example codeplug is provided in the configs directory).
2. Start `dvmconsole`.
3. Use "Open Codeplug" to open the configuration for the console.

## Project Notes

- Radio ID aliases and channel aliases MUST be under 12 characters long
- This project was forked from https://github.com/DVMProject/dvmconsole Thanks to the DVM team for all their hard work!!!

## License

This project is licensed under the AGPLv3 License - see the [LICENSE](LICENSE) file for details. Use of this project is intended, for amateur and/or educational use ONLY. Any other use is at the risk of user and all commercial purposes is strictly discouraged.
