import React from 'react';
import { motion } from 'framer-motion';
import { PlayerSetupPanel } from './PlayerSetupPanel';
import { GameManagementPanel } from './GameManagementPanel';
import { CONSTANTS } from '../../core/constants';

export const HomeScreen: React.FC = () => {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5 }}
      className="flex flex-col items-center w-full max-w-4xl mx-auto"
    >
      <header className="mb-8 text-center">
        <h1 className="text-4xl font-black text-clover-dark mb-2 drop-shadow-sm">
          🍀 So Clover!
        </h1>
        <p className="text-gray-600 font-medium">
          Prêt à faire fleurir vos indices ?
        </p>
      </header>

      <main className="w-full flex flex-col items-center">
        <PlayerSetupPanel />
        <GameManagementPanel />
      </main>

      <footer className="mt-12 text-gray-400 text-sm">
        SoClover Browser Game &copy; 2026 &mdash; v{CONSTANTS.APP_VERSION}
      </footer>
    </motion.div>
  );
};
