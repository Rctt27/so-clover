import React, { ReactNode } from 'react';
import { usePermissions } from '../../hooks/usePermissions';
import { Action } from '../../core/permissions';

interface RoleGuardProps {
  action: Action;
  children: ReactNode;
  fallback?: ReactNode;
  mode?: 'hide' | 'disable';
}

export const RoleGuard: React.FC<RoleGuardProps> = ({ 
  action, 
  children, 
  fallback = null, 
  mode = 'hide' 
}) => {
  const { can } = usePermissions();
  const hasPermission = can(action);

  if (hasPermission) {
    return <>{children}</>;
  }

  if (mode === 'disable') {
    return (
      <div style={{ opacity: 0.5, pointerEvents: 'none', cursor: 'not-allowed' }}>
        {children}
      </div>
    );
  }

  return <>{fallback}</>;
};
