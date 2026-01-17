import { useGameStore } from '../core/store';
import { canPerform, Action } from '../core/permissions';

export const usePermissions = () => {
  const { role, isGameAdmin, phase } = useGameStore();

  const can = (action: Action): boolean => {
    return canPerform({ role, isGameAdmin, phase }, action);
  };

  return {
    can,
    role,
    isGameAdmin,
    phase
  };
};
