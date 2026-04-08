import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { CONSTANTS } from '../core/constants';
import { isDebug } from '../core/debug';

class SignalRClient {
  private connection: HubConnection | null = null;
  private url: string;

  constructor() {
    this.url = CONSTANTS.SIGNALR_HUB_URL;
  }

  public getConnection(): HubConnection {
    if (!this.connection) {
      this.connection = new HubConnectionBuilder()
        .withUrl(this.url)
        .withAutomaticReconnect()
        .configureLogging(isDebug ? LogLevel.Debug : LogLevel.Warning)
        .build();
    }
    return this.connection;
  }

  public async start(): Promise<void> {
    const conn = this.getConnection();
    if (conn.state === HubConnectionState.Disconnected) {
      try {
        await conn.start();
        console.log('SignalR Connected');
      } catch (err) {
        console.error('SignalR Connection Error: ', err);
        throw err;
      }
    }
  }

  public async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  public on(eventName: string, callback: (...args: any[]) => void): void {
    this.getConnection().on(eventName, callback);
  }

  public off(eventName: string, callback?: (...args: any[]) => void): void {
    if (callback) {
      this.getConnection().off(eventName, callback);
    } else {
      this.getConnection().off(eventName);
    }
  }

  public async invoke(methodName: string, ...args: any[]): Promise<any> {
    return this.getConnection().invoke(methodName, ...args);
  }

  public get state(): HubConnectionState {
    return this.connection?.state ?? HubConnectionState.Disconnected;
  }
}

export const signalRClient = new SignalRClient();
