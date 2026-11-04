import {values} from '../../.generated/biwa-isolated.mjs';

/** Canonical bridge value types, shared by otherwise isolated VMs. */
export const Pair=values.Pair;
export const nil=values.nil;
export function unbox(value:unknown):unknown {return value===values.undef?undefined:value;}
