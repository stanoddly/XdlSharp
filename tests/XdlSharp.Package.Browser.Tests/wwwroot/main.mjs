import { dotnet } from './_framework/dotnet.js';

const exitCode = await dotnet.run();
process.exit(exitCode);
